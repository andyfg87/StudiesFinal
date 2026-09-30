using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Paginations;

namespace StudiesFinal.Web.Controllers
{
    public class AbstractEntityManagementController<TEntity, TKey, TInputViewModel, TDisplayViewModel> : Controller
        where TEntity : class, IEntity<TKey>
        where TInputViewModel : IEntityInputModel<TEntity, TKey>, new()
        where TDisplayViewModel : IEntityDisplayModel<TEntity, TKey>, new()
    {
        protected readonly IEntityRepository<TEntity, TKey> _repository;
        protected readonly IStringLocalizer _localizer;
        protected readonly IAppLogger _logger;

        public AbstractEntityManagementController(
            IEntityRepository<TEntity, TKey> repository,
            IStringLocalizer localizer,
            IAppLogger logger)
        {
            _repository = repository;
            _localizer = localizer;
            _logger = logger;
        }

        protected string GetEntityName() => typeof(TEntity).Name;
        protected string GetControllerName() => GetType().Name;

        // Métodos específicos para logging
        protected async Task LogInformation(string action, object? data = null)
        {
            var message = $"Se ejecutó {action} en {GetEntityName()}";
            await _logger.LogInformation(message, GetControllerName(), action, data);
        }

        protected async Task LogError(string action, Exception ex, object? data = null)
        {
            var message = $"Error al {action} en {GetEntityName()}";
            await _logger.LogError(message, ex, GetControllerName(), action, data);
        }

        public virtual async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string sortBy = "Id", string sortOrder = "asc")
        {
            var query = await _repository.GetAll();
            var paginatedData = await GetPaginatedData(query, pageNumber, pageSize);
            return View(paginatedData);
        }

        public virtual Task<IActionResult> Create()
        {
            return Task.FromResult<IActionResult>(View(new TInputViewModel())); // Inicializa el ViewModel
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Create(TInputViewModel inputViewModel)
        {
            if (!ModelState.IsValid)
                return View(inputViewModel);

            try
            {
                var entity = inputViewModel.Export();
                await _repository.AddAsync(entity);
                await _repository.SaveChangesAsync();

                await LogInformation(nameof(Create), inputViewModel);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await LogError(nameof(Create), ex, inputViewModel);
                ModelState.AddModelError("", _localizer["No se pudo crear el registro."]);
                return View(inputViewModel);
            }
        }

        public virtual async Task<IActionResult> Details(TKey key)
        {
            var entity = await _repository.GetByIdAsync(key);
            if (entity == null)
                return NotFound();

            var viewModel = new TDisplayViewModel();
            viewModel.Import(entity);

            return View(viewModel);
        }

        public virtual async Task<IActionResult> Edit(TKey key)
        {
            var entity = await _repository.GetByIdAsync(key);
            if (entity == null)
                return NotFound();

            var viewModel = new TInputViewModel();
            viewModel.Import(entity);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Edit(TInputViewModel inputModel)
        {
            if (!ModelState.IsValid)
                return View(inputModel);

            try
            {
                var entity = await _repository.GetByIdAsync(inputModel.Id);
                if (entity == null)
                    return NotFound();

                inputModel.Merge(entity);
                await _repository.UpdateAsync(entity);
                await _repository.SaveChangesAsync();

                await LogInformation(nameof(Edit), inputModel);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await LogError(nameof(Edit), ex, inputModel);
                ModelState.AddModelError("", _localizer["No se pudieron guardar los cambios."]);
                return View(inputModel);
            }
        }

        public virtual async Task<IActionResult> Delete(TKey key)
        {
            var entity = await _repository.GetByIdAsync(key);
            if (entity == null)
                return NotFound();

            var viewModel = new TDisplayViewModel();
            viewModel.Import(entity);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Delete(TDisplayViewModel displayModel)
        {
            await _repository.DeleteAsync(displayModel.Id);
            await _repository.SaveChangesAsync();

            await LogInformation(nameof(Delete), displayModel);

            return RedirectToAction(nameof(Index));
        }

        protected virtual async Task<PaginatedList<TDisplayViewModel>> GetPaginatedData(
            IQueryable<TEntity> source,
            int pageNumber = 1,
            int pageSize = 20,
            RouteValueDictionary? routeValues = null)
        {
            // Mantener los parámetros de búsqueda en la paginación
            routeValues ??= ViewBag.RouteValues as RouteValueDictionary ?? new RouteValueDictionary();

            // 0 = "Todos"
            if (pageSize <= 0) pageSize = int.MaxValue;
            if (pageNumber < 1) pageNumber = 1;

            var count = await source.CountAsync();
            var items = await source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModels = items.Select(item =>
            {
                var vm = new TDisplayViewModel();
                vm.Import(item);
                return vm;
            });

            return new PaginatedList<TDisplayViewModel>(viewModels, count, pageNumber, pageSize, routeValues);
        }
    }
}
