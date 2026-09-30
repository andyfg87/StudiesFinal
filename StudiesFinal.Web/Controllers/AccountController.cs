using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Models.ViewModels;
using System.Security.Claims;

namespace StudiesFinal.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IEntityRepository<User, Guid> _userRepo;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IAppLogger _logger;

        public AccountController(
            IEntityRepository<User, Guid> userRepo,
            IPasswordHasher<User> passwordHasher,
            IAppLogger logger)
        {
            _userRepo = userRepo;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        // ======================= LOGIN =======================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return LocalRedirect(returnUrl ?? "/");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginVM());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var username = model.Username.Trim();
            var user = await (await _userRepo.GetAll())
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || !user.IsActive ||
                _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
            {
                await _logger.LogWarning("Intento de acceso fallido", nameof(AccountController), nameof(Login), new { model.Username });
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos");
                return View(model);
            }

            await SignInUser(user, model.RememberMe);
            await _logger.LogInformation("Entrada al sistema", nameof(AccountController), nameof(Login), new { user.Username, Role = user.Role.ToString() });

            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }

        // ======================= CERRAR SESIÓN =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // ======================= ACCESO DENEGADO =======================
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ======================= CAMBIAR CONTRASEÑA =======================
        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordVM());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var id = User.UserId();
            var user = id.HasValue ? await _userRepo.GetByIdAsync(id.Value) : null;
            if (user == null)
                return RedirectToAction(nameof(Login));

            if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "La contraseña actual no es correcta");
                return View(model);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _userRepo.SaveChangesAsync();
            await _logger.LogInformation("Cambio de contraseña", nameof(AccountController), nameof(ChangePassword));

            TempData["Success"] = "Contraseña actualizada.";
            return RedirectToAction("Index", "Home");
        }

        // ======================= MÉTODOS AUXILIARES =======================
        private async Task SignInUser(User user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}
