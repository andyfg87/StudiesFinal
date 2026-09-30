using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using StudiesFinal.Web.Services;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    [Authorize(Roles = Roles.All)]
    public class StudiesController : AbstractEntityManagementController<Study, int, StudyInputVM, StudyDisplayVM>
    {
        private readonly IEntityRepository<Patient, int> _patientRepository;
        private readonly IEntityRepository<StudyTemplate, int> _templateRepository;
        private readonly IStudyFileService _files;
        private readonly IRichTextSanitizer _sanitizer;
        private readonly IReportPdfService _pdf;

        public StudiesController(
            IEntityRepository<Study, int> repository,
            IEntityRepository<Patient, int> patientRepository,
            IEntityRepository<StudyTemplate, int> templateRepository,
            IStudyFileService files,
            IRichTextSanitizer sanitizer,
            IReportPdfService pdf,
            IStringLocalizer<StudiesController> localizer,
            IAppLogger logger)
            : base(repository, localizer, logger)
        {
            _patientRepository = patientRepository;
            _templateRepository = templateRepository;
            _files = files;
            _sanitizer = sanitizer;
            _pdf = pdf;
        }

        // ======================= LISTADO POR FASE =======================
        public override async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string sortBy = "date", string sortOrder = "desc")
        {
            var q = Request.Query;
            var search = q["search"].ToString().Trim();
            var studyName = q["studyName"].ToString();
            DateTime? from = DateTime.TryParse(q["from"], out var f) ? f.Date : null;
            DateTime? to = DateTime.TryParse(q["to"], out var t) ? t.Date : null;

            // Fase: la del parámetro o, por defecto, la que le toca a cada rol
            var statusParam = q["status"].ToString();
            StudyStatus? status = Enum.TryParse<StudyStatus>(statusParam, true, out var s) ? s
                : statusParam == "all" ? null
                : User.IsInRole(Roles.Doctor) ? StudyStatus.ToSign : StudyStatus.InProgress;

            var baseQuery = await _repository.GetAll(includeProperties: "Patient,SignedBy");

            if (!string.IsNullOrEmpty(search))
            {
                var like = $"%{search}%";
                baseQuery = int.TryParse(search, out var number)
                    ? baseQuery.Where(x => x.PatientId == number || x.Id == number || EF.Functions.Like(x.Patient!.Name!, like))
                    : baseQuery.Where(x => EF.Functions.Like(x.Patient!.Name!, like) || EF.Functions.Like(x.StudyName!, like));
            }
            if (!string.IsNullOrEmpty(studyName))
                baseQuery = baseQuery.Where(x => x.StudyName == studyName);
            if (from.HasValue)
                baseQuery = baseQuery.Where(x => x.StudyDate >= from.Value);
            if (to.HasValue)
                baseQuery = baseQuery.Where(x => x.StudyDate < to.Value.AddDays(1));

            // Totales de las pestañas con los mismos filtros
            var byStatus = await baseQuery.GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            ViewBag.Counts = new StudyCounts
            {
                InProgress = byStatus.FirstOrDefault(x => x.Key == StudyStatus.InProgress)?.Count ?? 0,
                ToSign = byStatus.FirstOrDefault(x => x.Key == StudyStatus.ToSign)?.Count ?? 0,
                Completed = byStatus.FirstOrDefault(x => x.Key == StudyStatus.Completed)?.Count ?? 0
            };

            var query = status.HasValue ? baseQuery.Where(x => x.Status == status.Value) : baseQuery;

            var desc = sortOrder == "desc";
            query = sortBy switch
            {
                "id" => desc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
                "patient" => desc ? query.OrderByDescending(x => x.Patient!.Name) : query.OrderBy(x => x.Patient!.Name),
                "study" => desc ? query.OrderByDescending(x => x.StudyName) : query.OrderBy(x => x.StudyName),
                "signed" => desc ? query.OrderByDescending(x => x.SignedAt) : query.OrderBy(x => x.SignedAt),
                _ => desc ? query.OrderByDescending(x => x.StudyDate).ThenByDescending(x => x.Id)
                          : query.OrderBy(x => x.StudyDate).ThenBy(x => x.Id)
            };

            var routeValues = new RouteValueDictionary
            {
                ["status"] = status?.ToString() ?? "all",
                ["search"] = search,
                ["studyName"] = studyName,
                ["from"] = from?.ToString("yyyy-MM-dd"),
                ["to"] = to?.ToString("yyyy-MM-dd"),
                ["sortBy"] = sortBy,
                ["sortOrder"] = sortOrder
            };

            ViewBag.Status = status;
            ViewBag.StudyNames = await StudyNames();
            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentSortOrder = sortOrder;
            ViewBag.RouteValues = routeValues;

            return View(await GetPaginatedData(query, pageNumber, pageSize, routeValues));
        }

        // ======================= DETALLE / IMPRESIÓN =======================
        public override async Task<IActionResult> Details(int key)
        {
            var study = await LoadStudy(key);
            if (study == null)
                return NotFound();

            var vm = new StudyDisplayVM();
            vm.Import(study);

            ViewBag.CanEdit = StudyWorkflow.CanEdit(study, User);
            ViewBag.CanSendToSign = StudyWorkflow.CanSendToSign(study, User);
            ViewBag.CanReturn = StudyWorkflow.CanReturnToProgress(study, User);
            ViewBag.CanSign = StudyWorkflow.CanSign(study, User);
            ViewBag.CanUnlock = StudyWorkflow.CanUnlock(study, User);
            ViewBag.CanDelete = StudyWorkflow.CanDelete(study, User);

            return View(vm);
        }

        public async Task<IActionResult> Print(int id)
        {
            var study = await LoadStudy(id);
            if (study == null)
                return NotFound();

            var vm = new StudyDisplayVM();
            vm.Import(study);
            return View(vm);
        }

        // ======================= ALTA (solo en "En progreso") =======================
        public override async Task<IActionResult> Create()
        {
            var model = new StudyInputVM { StudyDate = DateTime.Today };

            if (int.TryParse(Request.Query["patientId"], out var patientId))
            {
                var patient = await _patientRepository.GetByIdAsync(patientId);
                if (patient != null)
                {
                    model.PatientId = patient.Id;
                    model.PatientLabel = $"{patient.Id} · {patient.Name}";
                }
            }

            if (int.TryParse(Request.Query["templateId"], out var templateId))
            {
                var template = await _templateRepository.GetByIdAsync(templateId);
                if (template != null)
                {
                    model.TemplateId = template.Id;
                    model.StudyName = template.GenericName ?? template.StudyTitle;
                    model.Information = _sanitizer.Sanitize(template.StudyInfo);
                }
            }

            model.Templates = await TemplateList(model.TemplateId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(600L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 600L * 1024 * 1024)]
        public override async Task<IActionResult> Create(StudyInputVM model)
        {
            var patient = model.PatientId.HasValue ? await _patientRepository.GetByIdAsync(model.PatientId.Value) : null;
            if (patient == null)
                ModelState.AddModelError(nameof(model.PatientId), "Selecciona un paciente existente");

            NormalizeLinks(model);

            if (!ModelState.IsValid)
                return await CreateView(model, patient);

            var study = model.Export(); // siempre nace "En progreso"
            study.Information = _sanitizer.Sanitize(model.Information);
            study.CreatedAt = DateTime.Now;
            study.CreatedByName = User.FullName();

            if (!await SaveUploads(model, study, patient!))
                return await CreateView(model, patient);

            try
            {
                await _repository.AddAsync(study);
                await _repository.SaveChangesAsync();
                await LogInformation(nameof(Create), new { study.Id, study.PatientId, study.StudyName });
            }
            catch (Exception ex)
            {
                await LogError(nameof(Create), ex, new { model.PatientId, model.StudyName });
                ModelState.AddModelError("", "No se pudo crear el estudio.");
                return await CreateView(model, patient);
            }

            if (Request.Form.ContainsKey("andSendToSign"))
                return await ChangeStatus(study.Id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Estudio creado y enviado a firmar.");

            TempData["Success"] = "Estudio creado.";
            return RedirectToAction(nameof(Details), new { key = study.Id });
        }

        // ======================= EDICIÓN =======================
        public override async Task<IActionResult> Edit(int key)
        {
            var study = await LoadStudy(key);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanEdit(study, User))
            {
                TempData["Error"] = study.IsLocked
                    ? "El estudio está firmado. Solo el doctor puede desbloquearlo para editarlo."
                    : "No tienes permiso para editar este estudio en su fase actual.";
                return RedirectToAction(nameof(Details), new { key });
            }

            var model = new StudyInputVM();
            model.Import(study);
            model.Information = _sanitizer.Sanitize(model.Information);
            ViewBag.CanSendToSign = StudyWorkflow.CanSendToSign(study, User);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(600L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 600L * 1024 * 1024)]
        public override async Task<IActionResult> Edit(StudyInputVM model)
        {
            var study = await _repository.GetByIdAsync(model.Id);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanEdit(study, User))
            {
                TempData["Error"] = "El estudio ya no se puede editar (puede que lo hayan firmado mientras tanto).";
                return RedirectToAction(nameof(Details), new { key = model.Id });
            }

            var patient = model.PatientId.HasValue ? await _patientRepository.GetByIdAsync(model.PatientId.Value) : null;
            if (patient == null)
                ModelState.AddModelError(nameof(model.PatientId), "Selecciona un paciente existente");

            NormalizeLinks(model);

            if (!ModelState.IsValid || !await SaveUploads(model, study, patient!))
            {
                model.Status = study.Status;
                model.PatientLabel = patient != null ? $"{patient.Id} · {patient.Name}" : null;
                ViewBag.CanSendToSign = StudyWorkflow.CanSendToSign(study, User);
                return View(model);
            }

            model.Merge(study);
            study.Information = _sanitizer.Sanitize(model.Information);
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await LogInformation(nameof(Edit), new { study.Id, study.PatientId, study.StudyName, Status = study.Status.ToString() });

            if (Request.Form.ContainsKey("andSendToSign"))
                return await ChangeStatus(study.Id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Cambios guardados y estudio enviado a firmar.");

            TempData["Success"] = "Cambios guardados.";
            return RedirectToAction(nameof(Details), new { key = study.Id });
        }

        // ======================= FLUJO DE TRABAJO =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SendToSign(int id)
            => ChangeStatus(id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Estudio enviado a firmar.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public Task<IActionResult> ReturnToProgress(int id)
            => ChangeStatus(id, StudyWorkflow.CanReturnToProgress, StudyStatus.InProgress, "Estudio devuelto a En progreso.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Doctor)]
        public async Task<IActionResult> Sign(int id, bool next = false)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanSign(study, User))
            {
                TempData["Error"] = "Este estudio no está pendiente de firma.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            study.Status = StudyStatus.Completed;
            study.SignedAt = DateTime.Now;
            study.SignedById = User.UserId();
            study.SignedByName = User.FullName();
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Estudio {id} firmado", nameof(StudiesController), nameof(Sign), new { id });

            // PDF del informe firmado en \\{Server}\{Share}\<tipo>\. Si falla, la firma se mantiene.
            var pdfError = await SaveSignedPdf(study);
            TempData["Success"] = pdfError == null
                ? $"Estudio firmado. PDF guardado en {study.SignedPdfPath}"
                : "Estudio firmado.";
            if (pdfError != null)
                TempData["Error"] = pdfError;

            // "Firmar y siguiente": abre el siguiente pendiente de firma
            if (next)
            {
                var nextId = await (await _repository.GetAll(x => x.Status == StudyStatus.ToSign))
                    .OrderBy(x => x.StudyDate).ThenBy(x => x.Id)
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync();
                if (nextId.HasValue)
                    return RedirectToAction(nameof(Details), new { key = nextId.Value });

                TempData["Success"] = TempData.Peek("Success") + " No quedan estudios por firmar.";
                return RedirectToAction(nameof(Index), new { status = nameof(StudyStatus.ToSign) });
            }

            return RedirectToAction(nameof(Details), new { key = id });
        }

        /// <summary>
        /// Vuelve a generar y guardar el PDF de un estudio firmado (si al firmar no se pudo
        /// guardar en el servidor, o para los estudios firmados en Access).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (study.Status != StudyStatus.Completed)
            {
                TempData["Error"] = "Solo se genera el PDF de estudios firmados.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var error = await SaveSignedPdf(study);
            if (error == null)
                TempData["Success"] = $"PDF guardado en {study.SignedPdfPath}";
            else
                TempData["Error"] = error;

            return RedirectToAction(nameof(Details), new { key = id });
        }

        /// <summary>PDF del informe generado al vuelo (vista previa, sin guardar en el servidor).</summary>
        public async Task<IActionResult> Pdf(int id)
        {
            var study = await LoadStudy(id);
            if (study == null)
                return NotFound();

            var bytes = _pdf.Generate(study);
            var name = Path.GetFileName(_files.FileNameFor(study.StudyName, study.Patient?.Name, ".pdf"));
            Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
            return File(bytes, "application/pdf");
        }

        /// <summary>Solo el doctor: quita la firma y lo devuelve a "Por firmar" para poder editarlo.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Doctor)]
        public async Task<IActionResult> Unlock(int id, string? reason)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanUnlock(study, User))
            {
                TempData["Error"] = "Solo el doctor puede desbloquear un estudio firmado.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var previous = new { study.SignedAt, study.SignedByName, study.SignedPdfPath };

            // El PDF anterior se queda en el servidor (histórico); al volver a firmar se genera otro
            study.Status = StudyStatus.ToSign;
            study.SignedAt = null;
            study.SignedById = null;
            study.SignedByName = null;
            study.SignedPdfPath = null;
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await _logger.LogWarning($"Estudio {id} desbloqueado", nameof(StudiesController), nameof(Unlock),
                new { id, reason, previous.SignedAt, previous.SignedByName, previous.SignedPdfPath });

            TempData["Success"] = "Estudio desbloqueado. Vuelve a estar Por firmar.";
            return RedirectToAction(nameof(Details), new { key = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> DeleteStudy(int id)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanDelete(study, User))
            {
                TempData["Error"] = "No se puede eliminar un estudio firmado.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var status = study.Status;
            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            await _logger.LogWarning($"Estudio {id} eliminado", nameof(StudiesController), nameof(DeleteStudy),
                new { id, study.PatientId, study.StudyName });

            TempData["Success"] = "Estudio eliminado.";
            return RedirectToAction(nameof(Index), new { status = status.ToString() });
        }

        // Las acciones genéricas de borrado no se usan: el borrado pasa por DeleteStudy
        [NonAction]
        public override Task<IActionResult> Delete(int key) => base.Delete(key);

        [NonAction]
        public override Task<IActionResult> Delete(StudyDisplayVM displayModel) => base.Delete(displayModel);

        // ======================= ARCHIVOS =======================
        /// <summary>
        /// Sirve el archivo enlazado desde el servidor de estudios. Solo rutas bajo
        /// \\{StudyFiles:Server}\{StudyFiles:Share}.
        /// </summary>
        public async Task<IActionResult> OpenFile(int id, int slot = 1)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            // slot 0 = PDF del informe firmado
            var path = _files.Normalize(slot switch { 0 => study.SignedPdfPath, 2 => study.LinkFile2, 3 => study.LinkFile3, _ => study.LinkFile1 });

            if (!_files.IsAllowed(path))
            {
                TempData["Error"] = $"La ruta del archivo no está dentro de {_files.BasePath}.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            try
            {
                if (!System.IO.File.Exists(path))
                {
                    TempData["Error"] = $"No se encuentra el archivo: {path}";
                    return RedirectToAction(nameof(Details), new { key = id });
                }
            }
            catch (Exception ex)
            {
                await LogError(nameof(OpenFile), ex, new { id, slot });
                TempData["Error"] = "El servidor no tiene acceso a la carpeta de estudios.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            if (!new FileExtensionContentTypeProvider().TryGetContentType(path!, out var contentType))
                contentType = "application/octet-stream";

            // inline: los PDF se abren en el navegador
            Response.Headers.ContentDisposition = $"inline; filename=\"{Path.GetFileName(path)}\"";
            return PhysicalFile(path!, contentType);
        }

        /// <summary>Ruta donde se guardaría un archivo subido ahora (para mostrarla en el formulario).</summary>
        [HttpGet]
        public async Task<IActionResult> UploadTarget(string? studyName, int? patientId)
        {
            var patient = patientId.HasValue ? await _patientRepository.GetByIdAsync(patientId.Value) : null;
            var path = Path.Combine(_files.FolderFor(studyName), _files.FileNameFor(studyName, patient?.Name, ".pdf"));
            return Json(new { path });
        }

        // ======================= AUXILIARES =======================
        /// <summary>
        /// Genera el PDF del informe firmado, lo guarda en la carpeta del reporte y
        /// apunta el estudio a él. Devuelve null si todo fue bien o el mensaje de error.
        /// </summary>
        private async Task<string?> SaveSignedPdf(Study study)
        {
            try
            {
                // Paciente y firmante para la cabecera y la firma del PDF
                study.Patient ??= await _patientRepository.GetByIdAsync(study.PatientId);

                var path = await _pdf.GenerateAndSaveAsync(study);
                study.SignedPdfPath = path;
                await _repository.SaveChangesAsync();

                await _logger.LogInformation($"PDF del estudio {study.Id} guardado", nameof(StudiesController), nameof(SaveSignedPdf), new { study.Id, path });
                return null;
            }
            catch (Exception ex)
            {
                await LogError(nameof(SaveSignedPdf), ex, new { study.Id });
                return $"No se pudo guardar el PDF en {_files.FolderFor(study.StudyName)}: {ex.Message} " +
                       "Puedes reintentarlo con \"Generar PDF\" cuando el servidor esté disponible.";
            }
        }
        private async Task<IActionResult> ChangeStatus(int id, Func<Study, System.Security.Claims.ClaimsPrincipal, bool> canChange, StudyStatus target, string message)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (!canChange(study, User))
            {
                TempData["Error"] = "No se puede cambiar la fase de este estudio.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var from = study.Status;
            study.Status = target;
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Estudio {id}: {from.Label()} → {target.Label()}", nameof(StudiesController), "ChangeStatus", new { id, from = from.ToString(), to = target.ToString() });

            TempData["Success"] = message;
            return RedirectToAction(nameof(Details), new { key = id });
        }

        private async Task<Study?> LoadStudy(int id)
            => await (await _repository.GetAll(x => x.Id == id, includeProperties: "Patient,SignedBy")).FirstOrDefaultAsync();

        private async Task<IActionResult> CreateView(StudyInputVM model, Patient? patient)
        {
            model.PatientLabel = patient != null ? $"{patient.Id} · {patient.Name}" : null;
            model.Templates = await TemplateList(model.TemplateId);
            return View(nameof(Create), model);
        }

        private async Task<SelectList> TemplateList(int? selected)
        {
            var templates = await (await _templateRepository.GetAll())
                .OrderBy(t => t.GenericName).ThenBy(t => t.StudyTitle)
                .Select(t => new { t.Id, t.StudyTitle })
                .ToListAsync();
            return new SelectList(templates, "Id", "StudyTitle", selected);
        }

        private async Task<List<string>> StudyNames()
            => await (await _repository.GetAll(x => x.StudyName != null && x.StudyName != ""))
                .Select(x => x.StudyName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

        /// <summary>Reescribe prefijos antiguos y comprueba que las rutas estén en el servidor de estudios.</summary>
        private void NormalizeLinks(StudyInputVM model)
        {
            model.LinkFile1 = _files.Normalize(model.LinkFile1);
            model.LinkFile2 = _files.Normalize(model.LinkFile2);
            model.LinkFile3 = _files.Normalize(model.LinkFile3);

            foreach (var (name, value) in new[] { (nameof(model.LinkFile1), model.LinkFile1), (nameof(model.LinkFile2), model.LinkFile2), (nameof(model.LinkFile3), model.LinkFile3) })
            {
                if (value != null && !_files.IsAllowed(value))
                    ModelState.AddModelError(name, $"La ruta debe empezar por {_files.BasePath}\\");
            }
        }

        /// <summary>Guarda los archivos subidos en el servidor y rellena LinkFileN. false si falla.</summary>
        private async Task<bool> SaveUploads(StudyInputVM model, Study study, Patient patient)
        {
            var uploads = new[] { (1, model.Upload1), (2, model.Upload2), (3, model.Upload3) };
            if (!uploads.Any(u => u.Item2?.Length > 0))
                return true;

            // Para el nombre del archivo se usan la fecha y el nombre del estudio del formulario
            var target = new Study { StudyDate = model.StudyDate, StudyName = model.StudyName };

            foreach (var (slot, file) in uploads)
            {
                if (file == null || file.Length == 0) continue;
                try
                {
                    var path = await _files.SaveAsync(file, target, patient, slot);
                    switch (slot)
                    {
                        case 1: model.LinkFile1 = path; break;
                        case 2: model.LinkFile2 = path; break;
                        case 3: model.LinkFile3 = path; break;
                    }
                }
                catch (Exception ex)
                {
                    await LogError("Upload", ex, new { slot, file.FileName });
                    ModelState.AddModelError($"Upload{slot}", $"No se pudo guardar el archivo en {_files.FolderFor(model.StudyName)}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }
    }
}
