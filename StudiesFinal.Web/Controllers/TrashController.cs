using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    /// <summary>
    /// "Deleted items": estudios, pacientes, plantillas y usuarios eliminados (borrado lógico),
    /// con quién y cuándo, y la opción de restaurarlos. Solo administradores.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    public class TrashController : Controller
    {
        private const int MaxRows = 500;

        private readonly IEntityRepository<Study, int> _studies;
        private readonly IEntityRepository<Patient, int> _patients;
        private readonly IEntityRepository<StudyTemplate, int> _templates;
        private readonly IEntityRepository<User, Guid> _users;
        private readonly IAppLogger _logger;

        public TrashController(
            IEntityRepository<Study, int> studies,
            IEntityRepository<Patient, int> patients,
            IEntityRepository<StudyTemplate, int> templates,
            IEntityRepository<User, Guid> users,
            IAppLogger logger)
        {
            _studies = studies;
            _patients = patients;
            _templates = templates;
            _users = users;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new TrashVM
            {
                Studies = await (await _studies.GetDeleted(nameof(Study.Patient)))
                    .OrderByDescending(s => s.DeletedAt).Take(MaxRows).ToListAsync(),
                Patients = await (await _patients.GetDeleted())
                    .OrderByDescending(p => p.DeletedAt).Take(MaxRows).ToListAsync(),
                Templates = await (await _templates.GetDeleted())
                    .OrderByDescending(t => t.DeletedAt).Take(MaxRows).ToListAsync(),
                Users = await (await _users.GetDeleted())
                    .OrderByDescending(u => u.DeletedAt).Take(MaxRows).ToListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreStudy(int id)
        {
            var study = await (await _studies.GetDeleted()).FirstOrDefaultAsync(s => s.Id == id);
            if (study == null || !await _studies.RestoreAsync(id))
                return NotFoundMessage("study");

            // Un estudio no puede quedar visible con su paciente eliminado: se restaura también
            var patientRestored = await _patients.RestoreAsync(study.PatientId);

            await _studies.SaveChangesAsync();
            await _logger.LogInformation($"Study restored: {id}" + (patientRestored ? $" (and patient {study.PatientId})" : ""),
                nameof(TrashController), nameof(RestoreStudy));

            TempData["Success"] = patientRestored
                ? $"Study {id} restored. Its patient was deleted too and has been restored."
                : $"Study {id} restored.";
            return RedirectToAction(nameof(Index), null, null, "studies");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestorePatient(int id)
        {
            if (!await _patients.RestoreAsync(id))
                return NotFoundMessage("patient");

            await _patients.SaveChangesAsync();
            await _logger.LogInformation($"Patient restored: {id}", nameof(TrashController), nameof(RestorePatient));

            TempData["Success"] = $"Patient {id} restored.";
            return RedirectToAction(nameof(Index), null, null, "patients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreTemplate(int id)
        {
            if (!await _templates.RestoreAsync(id))
                return NotFoundMessage("template");

            await _templates.SaveChangesAsync();
            await _logger.LogInformation($"Template restored: {id}", nameof(TrashController), nameof(RestoreTemplate));

            TempData["Success"] = "Template restored.";
            return RedirectToAction(nameof(Index), null, null, "templates");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreUser(Guid id)
        {
            var user = await (await _users.GetDeleted()).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFoundMessage("user");

            // El nombre de usuario es único entre los activos: puede que ya lo use otro
            if (await (await _users.GetAll()).AnyAsync(u => u.Username == user.Username))
            {
                TempData["Error"] = $"User {user.Username} cannot be restored: another user already has that username.";
                return RedirectToAction(nameof(Index), null, null, "users");
            }

            await _users.RestoreAsync(id);
            await _users.SaveChangesAsync();
            await _logger.LogInformation($"User restored: {user.Username}", nameof(TrashController), nameof(RestoreUser));

            TempData["Success"] = $"User {user.Username} restored.";
            return RedirectToAction(nameof(Index), null, null, "users");
        }

        private IActionResult NotFoundMessage(string what)
        {
            TempData["Error"] = $"The {what} was not found or is not deleted.";
            return RedirectToAction(nameof(Index));
        }
    }

}
