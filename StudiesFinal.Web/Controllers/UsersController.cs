using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class UsersController : AbstractEntityManagementController<User, Guid, UserInputVM, UserDisplayVM>
    {
        private readonly IPasswordHasher<User> _passwordHasher;

        public UsersController(
            IEntityRepository<User, Guid> repository,
            IPasswordHasher<User> passwordHasher,
            IStringLocalizer<UsersController> localizer,
            IAppLogger logger)
            : base(repository, localizer, logger)
        {
            _passwordHasher = passwordHasher;
        }

        public override async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string sortBy = "name", string sortOrder = "asc")
        {
            var search = Request.Query["search"].ToString().Trim();
            var query = await _repository.GetAll();

            if (!string.IsNullOrEmpty(search))
            {
                var like = $"%{search}%";
                query = query.Where(u => EF.Functions.Like(u.Username, like)
                                      || EF.Functions.Like(u.FirstName + " " + u.LastName, like));
            }

            var desc = sortOrder == "desc";
            query = sortBy switch
            {
                "user" => desc ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
                "role" => desc ? query.OrderByDescending(u => u.Role) : query.OrderBy(u => u.Role),
                _ => desc ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName)
                          : query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            };

            ViewBag.CurrentSortBy = sortBy;
            ViewBag.CurrentSortOrder = sortOrder;
            ViewBag.RouteValues = new RouteValueDictionary { ["search"] = search, ["sortBy"] = sortBy, ["sortOrder"] = sortOrder };

            return View(await GetPaginatedData(query, pageNumber, pageSize));
        }

        public override Task<IActionResult> Create()
        {
            return Task.FromResult<IActionResult>(View(new UserInputVM { AvailableRoles = GetRoles() }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Create(UserInputVM model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError(nameof(model.Password), "Password is required");

            if (await UsernameExists(model.UserName, null))
                ModelState.AddModelError(nameof(model.UserName), "A user with that username already exists");

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = GetRoles();
                return View(model);
            }

            var user = model.Export();
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password!);

            await _repository.AddAsync(user);
            await _repository.SaveChangesAsync();
            await LogInformation(nameof(Create), model);

            TempData["Success"] = $"User {user.Username} created.";
            return RedirectToAction(nameof(Index));
        }

        public override async Task<IActionResult> Edit(Guid key)
        {
            var entity = await _repository.GetByIdAsync(key);
            if (entity == null)
                return NotFound();

            var model = new UserInputVM();
            model.Import(entity);
            model.AvailableRoles = GetRoles();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Edit(UserInputVM model)
        {
            var user = await _repository.GetByIdAsync(model.Id);
            if (user == null)
                return NotFound();

            if (await UsernameExists(model.UserName, model.Id))
                ModelState.AddModelError(nameof(model.UserName), "A user with that username already exists");

            // No quitarse a uno mismo el rol de administrador ni desactivarse
            if (user.Id == User.UserId() && (model.Role != UserRole.Admin || !model.IsActive))
                ModelState.AddModelError(nameof(model.Role), "You cannot remove your own administrator role or deactivate your own user.");

            if (!ModelState.IsValid)
            {
                model.IsEdit = true;
                model.AvailableRoles = GetRoles();
                return View(model);
            }

            model.Merge(user);
            if (!string.IsNullOrWhiteSpace(model.Password))
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            await _repository.SaveChangesAsync();
            await LogInformation(nameof(Edit), model);

            TempData["Success"] = $"User {user.Username} updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            if (id == User.UserId())
            {
                TempData["Error"] = "You cannot delete your own user.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _repository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            try
            {
                await _repository.DeleteAsync(id);
                await _repository.SaveChangesAsync();
                await _logger.LogInformation($"User deleted: {user.Username}", nameof(UsersController), nameof(DeleteUser));
                TempData["Success"] = $"User {user.Username} deleted.";
            }
            catch (Exception ex)
            {
                await LogError(nameof(DeleteUser), ex, new { id });
                TempData["Error"] = "The user could not be deleted. You can deactivate it instead.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> UsernameExists(string username, Guid? exceptId)
        {
            var name = username.Trim();
            return await (await _repository.GetAll())
                .AnyAsync(u => u.Username == name && (!exceptId.HasValue || u.Id != exceptId.Value));
        }

        private static SelectList GetRoles()
        {
            return new SelectList(Enum.GetValues<UserRole>()
                .Select(r => new { Value = r, Text = r.RoleLabel() }),
                "Value", "Text");
        }
    }
}
