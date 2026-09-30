using System.Security.Claims;

namespace StudiesFinal.Web.Extensions
{
    public static class ClaimsExtensions
    {
        public static Guid? UserId(this ClaimsPrincipal user)
            => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public static string FullName(this ClaimsPrincipal user)
            => user.FindFirstValue("FullName") ?? user.Identity?.Name ?? "Desconocido";
    }
}
