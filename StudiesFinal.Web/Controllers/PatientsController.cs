using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using StudiesFinal.Web.Paginations;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    [Authorize(Roles = Roles.All)]
    public class PatientsController : AbstractEntityManagementController<Patient, int, PatientInputVM, PatientDisplayVM>
    {
        private readonly IEntityRepository<Study, int> _studyRepository;

        public PatientsController(
            IEntityRepository<Patient, int> repository,
            IEntityRepository<Study, int> studyRepository,
            IStringLocalizer<PatientsController> localizer,
            IAppLogger logger)
            : base(repository, localizer, logger)
        {
            _studyRepository = studyRepository;
        }

        public override async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string sortBy = "name", string sortOrder = "asc")
        {
            var search = Request.Query["search"].ToString().Trim();
            DateTime? dob = DateTime.TryParse(Request.Query["dob"], out var b) ? b.Date : null;
            var query = await _repository.GetAll();

            if (!string.IsNullOrEmpty(search))
            {
                if (int.TryParse(search, out var id))
                    query = query.Where(p => p.Id == id || EF.Functions.Like(p.Name!, $"%{search}%"));
                else
                    query = query.Where(p => EF.Functions.Like(p.Name!, $"%{search}%"));
            }

            if (dob.HasValue)
                query = query.Where(p => p.DateOfBirth >= dob.Value && p.DateOfBirth < dob.Value.AddDays(1));

            // Proyección directa: cuenta los estudios sin cargarlos
            var projected = query.Select(p => new PatientDisplayVM
            {
                Id = p.Id,
                Name = p.Name,
                DateOfBirth = p.DateOfBirth,
                StudyCount = p.Studies.Count(),
                LastStudyDate = p.Studies.Max(s => (DateTime?)s.StudyDate)
            });

            var desc = sortOrder == "desc";
            projected = sortBy switch
            {
                "id" => desc ? projected.OrderByDescending(p => p.Id) : projected.OrderBy(p => p.Id),
                "dob" => desc ? projected.OrderByDescending(p => p.DateOfBirth) : projected.OrderBy(p => p.DateOfBirth),
                "studies" => desc ? projected.OrderByDescending(p => p.StudyCount) : projected.OrderBy(p => p.StudyCount),
                "last" => desc ? projected.OrderByDescending(p => p.LastStudyDate) : projected.OrderBy(p => p.LastStudyDate),
                _ => desc ? projected.OrderByDescending(p => p.Name) : projected.OrderBy(p => p.Name)
            };

            var routeValues = new RouteValueDictionary
            {
                ["search"] = search, ["dob"] = dob?.ToString("yyyy-MM-dd"), ["sortBy"] = sortBy, ["sortOrder"] = sortOrder
            };
            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentSortOrder = sortOrder;
            ViewBag.RouteValues = routeValues;

            return View(await PaginatedList<PatientDisplayVM>.CreateAsync(projected, pageNumber, pageSize, routeValues));
        }

        public override async Task<IActionResult> Details(int key)
        {
            var patient = await _repository.GetByIdAsync(key);
            if (patient == null)
                return NotFound();

            var studies = await (await _studyRepository.GetAll(s => s.PatientId == key, includeProperties: "SignedBy"))
                .OrderByDescending(s => s.StudyDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync();

            patient.Studies = studies;
            var vm = new PatientDisplayVM();
            vm.Import(patient);

            ViewBag.Studies = studies.Select(s => { var d = new StudyDisplayVM(); d.Import(s); return d; }).ToList();
            return View(vm);
        }

        public override async Task<IActionResult> Create()
        {
            // Propuesta: siguiente número libre (se puede cambiar por el que dé el equipo)
            var maxId = await (await _repository.GetAll()).MaxAsync(p => (int?)p.Id) ?? 0;
            return View(new PatientInputVM { Id = maxId + 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Create(PatientInputVM model)
        {
            if (await _repository.GetByIdAsync(model.Id) != null)
                ModelState.AddModelError(nameof(model.Id), "A patient with that number already exists");

            if (!ModelState.IsValid)
                return View(model);

            await _repository.AddAsync(model.Export());
            await _repository.SaveChangesAsync();
            await LogInformation(nameof(Create), model);

            TempData["Success"] = "Patient created.";

            // Si venía del alta de un estudio, vuelve allí con el paciente elegido
            if (Request.Query["returnToStudy"] == "1")
                return RedirectToAction("Create", "Studies", new { patientId = model.Id });

            return RedirectToAction(nameof(Details), new { key = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Edit(PatientInputVM model)
        {
            model.IsEdit = true;
            if (!ModelState.IsValid)
                return View(model);

            var entity = await _repository.GetByIdAsync(model.Id);
            if (entity == null)
                return NotFound();

            model.Merge(entity);
            await _repository.SaveChangesAsync();
            await LogInformation(nameof(Edit), model);

            TempData["Success"] = "Patient updated.";
            return RedirectToAction(nameof(Details), new { key = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var hasStudies = await (await _studyRepository.GetAll(s => s.PatientId == id)).AnyAsync();
            if (hasStudies)
            {
                TempData["Error"] = "The patient cannot be deleted because they have studies.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Patient deleted: {id}", nameof(PatientsController), nameof(DeletePatient));

            TempData["Success"] = "Patient deleted.";
            return RedirectToAction(nameof(Index));
        }

        private static readonly string[] DobFormats = { "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy", "yyyy-MM-dd" };

        /// <summary>
        /// Búsqueda para el selector de paciente (JSON): por nombre, nº de paciente
        /// o fecha de nacimiento (MM/dd/yyyy).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(string? q)
        {
            q = q?.Trim();
            if (string.IsNullOrEmpty(q))
                return Json(Array.Empty<object>());

            var query = await _repository.GetAll();
            if (DateTime.TryParseExact(q, DobFormats, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dob))
                query = query.Where(p => p.DateOfBirth >= dob.Date && p.DateOfBirth < dob.Date.AddDays(1));
            else if (int.TryParse(q, out var id))
                query = query.Where(p => p.Id == id || EF.Functions.Like(p.Name!, $"%{q}%"));
            else
                query = query.Where(p => EF.Functions.Like(p.Name!, $"%{q}%"));

            var results = await query
                .OrderBy(p => p.Name)
                .Take(20)
                .Select(p => new { id = p.Id, name = p.Name, dob = p.DateOfBirth })
                .ToListAsync();

            return Json(results.Select(p => new
            {
                p.id,
                p.name,
                dob = p.dob?.ToString("MM/dd/yyyy")
            }));
        }
    }
}
