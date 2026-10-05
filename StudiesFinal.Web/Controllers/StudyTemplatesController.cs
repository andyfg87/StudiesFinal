using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using StudiesFinal.Web.Services;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    /// <summary>
    /// Plantillas de informe. Todos pueden verlas y usarlas; solo Doctor y Admin
    /// pueden crearlas, modificarlas o borrarlas (los técnicos no).
    /// </summary>
    [Authorize(Roles = Roles.All)]
    public class StudyTemplatesController : AbstractEntityManagementController<StudyTemplate, int, StudyTemplateInputVM, StudyTemplateDisplayVM>
    {
        private readonly IRichTextSanitizer _sanitizer;

        public StudyTemplatesController(
            IEntityRepository<StudyTemplate, int> repository,
            IRichTextSanitizer sanitizer,
            IStringLocalizer<StudyTemplatesController> localizer,
            IAppLogger logger)
            : base(repository, localizer, logger)
        {
            _sanitizer = sanitizer;
        }

        public override async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 0, string sortBy = "title", string sortOrder = "asc")
        {
            var query = await _repository.GetAll(orderBy: q => q.OrderBy(t => t.GenericName).ThenBy(t => t.StudyTitle));
            ViewBag.RouteValues = new RouteValueDictionary { ["pageNumber"] = pageNumber, ["pageSize"] = pageSize };
            return View(await GetPaginatedData(query, pageNumber, pageSize));
        }

        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Create() => base.Create();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Create(StudyTemplateInputVM inputViewModel)
        {
            inputViewModel.StudyInfo = _sanitizer.Sanitize(inputViewModel.StudyInfo);
            return base.Create(inputViewModel);
        }

        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Edit(int key) => base.Edit(key);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Edit(StudyTemplateInputVM inputModel)
        {
            inputModel.StudyInfo = _sanitizer.Sanitize(inputModel.StudyInfo);
            return base.Edit(inputModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var template = await _repository.GetByIdAsync(id);
            if (template == null)
                return NotFound();

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            await _logger.LogInformation($"Template deleted: {template.StudyTitle}", nameof(StudyTemplatesController), nameof(DeleteTemplate));

            TempData["Success"] = "Template deleted.";
            return RedirectToList();
        }

        /// <summary>Contenido de la plantilla para rellenar un estudio nuevo (JSON).</summary>
        [HttpGet]
        public async Task<IActionResult> TemplateContent(int id)
        {
            var template = await _repository.GetByIdAsync(id);
            if (template == null)
                return NotFound();

            return Json(new
            {
                studyName = template.GenericName ?? template.StudyTitle,
                info = _sanitizer.Sanitize(template.StudyInfo) ?? ""
            });
        }

        // Los técnicos no pueden borrar plantillas por la acción genérica
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Delete(int key) => base.Delete(key);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Roles.AdminOrDoctor)]
        public override Task<IActionResult> Delete(StudyTemplateDisplayVM displayModel) => base.Delete(displayModel);
    }
}
