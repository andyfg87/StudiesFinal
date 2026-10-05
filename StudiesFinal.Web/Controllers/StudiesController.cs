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
            DateTime? dob = DateTime.TryParse(q["dob"], out var b) ? b.Date : null;

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
                    : baseQuery.Where(x => EF.Functions.Like(x.Patient!.Name!, like) || EF.Functions.Like(x.Patient!.LastName!, like) || EF.Functions.Like(x.StudyName!, like));
            }
            if (dob.HasValue)
                baseQuery = baseQuery.Where(x => x.Patient!.DateOfBirth >= dob.Value && x.Patient!.DateOfBirth < dob.Value.AddDays(1));
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
                ["dob"] = dob?.ToString("yyyy-MM-dd"),
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
                    model.PatientLabel = $"{patient.Id} · {patient.LastName ?? ""} · {patient.Name}";
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
        public override async Task<IActionResult> Create(StudyInputVM model)
        {
            var patient = model.PatientId.HasValue ? await _patientRepository.GetByIdAsync(model.PatientId.Value) : null;
            if (patient == null)
                ModelState.AddModelError(nameof(model.PatientId), "Select an existing patient");

            ApplyFileSelections(model, existing: null);

            if (!ModelState.IsValid)
                return await CreateView(model, patient);

            var study = model.Export(); // siempre nace "En progreso"
            study.Information = _sanitizer.Sanitize(model.Information);
            study.CreatedAt = DateTime.Now;
            study.CreatedByName = User.FullName();

            try
            {
                await _repository.AddAsync(study);
                await _repository.SaveChangesAsync();
                await LogInformation(nameof(Create), new { study.Id, study.PatientId, study.StudyName });
            }
            catch (Exception ex)
            {
                await LogError(nameof(Create), ex, new { model.PatientId, model.StudyName });
                ModelState.AddModelError("", "The study could not be created.");
                return await CreateView(model, patient);
            }

            if (Request.Form.ContainsKey("andSendToSign"))
                return await ChangeStatus(study.Id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Study created and sent to sign.");

            TempData["Success"] = "Study created.";
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
                    ? "This study is signed. Only the doctor can unlock it for editing."
                    : "You are not allowed to edit this study in its current phase.";
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
        public override async Task<IActionResult> Edit(StudyInputVM model)
        {
            var study = await _repository.GetByIdAsync(model.Id);
            if (study == null)
                return NotFound();

            if (!StudyWorkflow.CanEdit(study, User))
            {
                TempData["Error"] = "This study can no longer be edited (it may have been signed in the meantime).";
                return RedirectToAction(nameof(Details), new { key = model.Id });
            }

            var patient = model.PatientId.HasValue ? await _patientRepository.GetByIdAsync(model.PatientId.Value) : null;
            if (patient == null)
                ModelState.AddModelError(nameof(model.PatientId), "Select an existing patient");

            ApplyFileSelections(model, study);

            if (!ModelState.IsValid)
            {
                model.Status = study.Status;
                // Los archivos ya guardados siguen visibles para consultarlos al corregir
                var saved = new StudyInputVM();
                saved.Import(study);
                model.SavedFiles = saved.SavedFiles;
                model.PatientLabel = patient != null ? $"{patient.Id} · {patient.LastName} · {patient.Name}" : null;
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
                return await ChangeStatus(study.Id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Changes saved and study sent to sign.");

            TempData["Success"] = "Changes saved.";
            return RedirectToAction(nameof(Details), new { key = study.Id });
        }

        // ======================= FLUJO DE TRABAJO =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SendToSign(int id)
            => ChangeStatus(id, StudyWorkflow.CanSendToSign, StudyStatus.ToSign, "Study sent to sign.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public Task<IActionResult> ReturnToProgress(int id)
            => ChangeStatus(id, StudyWorkflow.CanReturnToProgress, StudyStatus.InProgress, "Study returned to In progress.");

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
                TempData["Error"] = "This study is not waiting to be signed.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            study.Status = StudyStatus.Completed;
            study.SignedAt = DateTime.Now;
            study.SignedById = User.UserId();
            study.SignedByName = User.FullName();
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Study {id} signed", nameof(StudiesController), nameof(Sign), new { id });

            // PDF del informe firmado en \\{Server}\{Share}\<tipo>\. Si falla, la firma se mantiene.
            var pdfError = await SaveSignedPdf(study);
            TempData["Success"] = pdfError == null
                ? "Study signed. The signed PDF was saved on the studies server."
                : "Study signed.";
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

                TempData["Success"] = TempData.Peek("Success") + " There are no more studies to sign.";
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
                TempData["Error"] = "A PDF can only be generated for signed studies.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var error = await SaveSignedPdf(study);
            if (error == null)
                TempData["Success"] = "The signed PDF was saved on the studies server.";
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
                TempData["Error"] = "Only the doctor can unlock a signed study.";
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
            await _logger.LogWarning($"Study {id} unlocked", nameof(StudiesController), nameof(Unlock),
                new { id, reason, previous.SignedAt, previous.SignedByName, previous.SignedPdfPath });

            TempData["Success"] = "Study unlocked. It is back in To sign.";
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
                TempData["Error"] = "A signed study cannot be deleted.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var status = study.Status;
            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            await _logger.LogWarning($"Study {id} deleted", nameof(StudiesController), nameof(DeleteStudy),
                new { id, study.PatientId, study.StudyName });

            TempData["Success"] = "Study deleted.";
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
        /// download = false: se abre en el visor del navegador (ventana o pestaña nueva).
        /// download = true: se descarga y se abre con el lector de PDF instalado.
        /// Siempre se abre en otra ventana, así que los errores se muestran en una
        /// página propia (sin la ruta) y no en la del estudio.
        /// </summary>
        public async Task<IActionResult> OpenFile(int id, int slot = 1, bool download = false)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            // slot 0 = PDF del informe firmado. Los prefijos antiguos de Access (Z:\Studies…)
            // se traducen al servidor actual; lo que quede fuera de la carpeta de estudios no se abre.
            var path = _files.Normalize(slot switch { 0 => study.SignedPdfPath, 2 => study.LinkFile2, 3 => study.LinkFile3, _ => study.LinkFile1 });
            if (path != null && !_files.IsAllowed(path))
            {
                await _logger.LogWarning($"Study {id}: file outside the studies folder", nameof(StudiesController), nameof(OpenFile), new { id, slot, path });
                return StudyFileResults.FileError(this, "This file cannot be opened from the application.", Path.GetFileName(path));
            }

            return await this.ServeStudyFile(path, download, _logger);
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

                await _logger.LogInformation($"PDF of study {study.Id} saved", nameof(StudiesController), nameof(SaveSignedPdf), new { study.Id, path });
                return null;
            }
            catch (Exception ex)
            {
                await LogError(nameof(SaveSignedPdf), ex, new { study.Id });
                return "The signed PDF could not be saved on the studies server. " +
                       "You can retry with \"Generate PDF\" when the server is available.";
            }
        }
        private async Task<IActionResult> ChangeStatus(int id, Func<Study, System.Security.Claims.ClaimsPrincipal, bool> canChange, StudyStatus target, string message)
        {
            var study = await _repository.GetByIdAsync(id);
            if (study == null)
                return NotFound();

            if (!canChange(study, User))
            {
                TempData["Error"] = "The phase of this study cannot be changed.";
                return RedirectToAction(nameof(Details), new { key = id });
            }

            var from = study.Status;
            study.Status = target;
            study.UpdatedAt = DateTime.Now;
            study.UpdatedByName = User.FullName();

            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Study {id}: {from.Label()} → {target.Label()}", nameof(StudiesController), "ChangeStatus", new { id, from = from.ToString(), to = target.ToString() });

            TempData["Success"] = message;
            return RedirectToAction(nameof(Details), new { key = id });
        }

        private async Task<Study?> LoadStudy(int id)
            => await (await _repository.GetAll(x => x.Id == id, includeProperties: "Patient,SignedBy")).FirstOrDefaultAsync();

        private async Task<IActionResult> CreateView(StudyInputVM model, Patient? patient)
        {
            model.PatientLabel = patient != null ? $"{patient.Id} · {patient.LastName ?? ""} · {patient.Name}" : null;
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

        /// <summary>
        /// Aplica los archivos elegidos en el explorador (no se sube nada: solo se guarda la ruta).
        /// Por cada hueco, FileSelectionN es:
        ///   vacío -> se mantiene el archivo actual (p. ej. la ruta importada tal cual de Access)
        ///   "-"   -> se quita el archivo
        ///   ruta del explorador ("Ubicación\carpeta\archivo") -> nuevo archivo
        /// Las rutas nunca vienen del formulario directamente: LinkFileN no se enlaza (BindNever).
        /// </summary>
        /// <param name="existing">Estudio guardado (en edición); null al crear.</param>
        private void ApplyFileSelections(StudyInputVM model, Study? existing)
        {
            string? Apply(int slot, string? selection, string? current)
            {
                if (string.IsNullOrWhiteSpace(selection)) return current;
                if (selection == StudyInputVM.RemoveFile) return null;

                // "Ubicación\...\archivo": tiene que haber algo más que el nombre de la ubicación
                var full = selection.Trim('\\', '/').Contains('\\') ? _files.Resolve(selection) : null;
                if (full == null)
                {
                    ModelState.AddModelError($"FileSelection{slot}", "The selected file is not valid. Choose it again.");
                    return current;
                }
                return full;
            }

            model.LinkFile1 = Apply(1, model.FileSelection1, existing?.LinkFile1);
            model.LinkFile2 = Apply(2, model.FileSelection2, existing?.LinkFile2);
            model.LinkFile3 = Apply(3, model.FileSelection3, existing?.LinkFile3);
        }
    }
}
