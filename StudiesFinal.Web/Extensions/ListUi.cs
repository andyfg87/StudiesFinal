using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using StudiesFinal.Models.Entities;
using System.Text.Encodings.Web;

namespace StudiesFinal.Web.Extensions
{
    /// <summary>
    /// Helpers comunes para los listados (Index) con el estilo de capote-list.css.
    /// </summary>
    public static class ListUi
    {
        /// <summary>
        /// Enlace de cabecera ordenable. Usa ViewBag.RouteValues, ViewBag.CurrentSortBy
        /// y ViewBag.CurrentSortOrder, que rellenan los controladores.
        /// Uso: &lt;th&gt;@Html.SortHeader("Paciente", "patient")&lt;/th&gt;
        /// </summary>
        public static IHtmlContent SortHeader(this IHtmlHelper html, string label, string column, string action = "Index")
        {
            var viewBag = html.ViewBag;
            var routeValues = viewBag.RouteValues as RouteValueDictionary ?? new RouteValueDictionary();
            string currentBy = viewBag.CurrentSortBy as string ?? "";
            string currentOrder = viewBag.CurrentSortOrder as string ?? "asc";

            var active = string.Equals(currentBy, column, StringComparison.OrdinalIgnoreCase);

            var rv = new RouteValueDictionary(routeValues)
            {
                ["sortBy"] = column,
                ["sortOrder"] = active && currentOrder == "asc" ? "desc" : "asc",
                ["pageNumber"] = "1" // al cambiar el orden se vuelve a la primera página
            };

            var urlHelper = html.ViewContext.HttpContext.RequestServices
                .GetRequiredService<IUrlHelperFactory>()
                .GetUrlHelper(html.ViewContext);

            var href = urlHelper.Action(action, rv) ?? "#";

            var icon = !active ? "bi-chevron-expand"
                     : currentOrder == "asc" ? "bi-caret-up-fill" : "bi-caret-down-fill";

            var title = !active ? "Sort"
                      : currentOrder == "asc" ? "Ascending" : "Descending";

            var enc = HtmlEncoder.Default;
            return new HtmlString(
                $"<a class=\"ls-sort{(active ? " is-active" : "")}\" href=\"{enc.Encode(href)}\" title=\"{title}\" style=\"color:inherit;text-decoration:{(active ? "underline" : "none")}\">" +
                $"{enc.Encode(label)} <i class=\"bi {icon}\" aria-hidden=\"true\"></i></a>");
        }

        // ---- Estados de estudio ---------------------------------------------

        public static string Label(this StudyStatus status) => status switch
        {
            StudyStatus.InProgress => "In progress",
            StudyStatus.ToSign => "To sign",
            StudyStatus.Completed => "Completed",
            _ => status.ToString()
        };

        public static string PillClass(this StudyStatus status) => status switch
        {
            StudyStatus.InProgress => "warning",
            StudyStatus.ToSign => "info",
            StudyStatus.Completed => "success",
            _ => "primary"
        };

        public static string Icon(this StudyStatus status) => status switch
        {
            StudyStatus.InProgress => "bi-hourglass-split",
            StudyStatus.ToSign => "bi-pen",
            StudyStatus.Completed => "bi-patch-check",
            _ => "bi-file-medical"
        };

        public static IHtmlContent StatusPill(this IHtmlHelper html, StudyStatus status)
            => new HtmlString($"<span class=\"ls-pill {status.PillClass()}\"><i class=\"bi {status.Icon()}\" aria-hidden=\"true\"></i>{status.Label()}</span>");

        public static string RoleLabel(this UserRole role) => role switch
        {
            UserRole.Admin => "Administrator",
            UserRole.Doctor => "Doctor",
            UserRole.Technician => "Technician",
            _ => role.ToString()
        };

        /// <summary>Edad en años a partir de la fecha de nacimiento.</summary>
        public static int? Age(DateTime? dob, DateTime? at = null)
        {
            if (!dob.HasValue) return null;
            var today = (at ?? DateTime.Today).Date;
            var age = today.Year - dob.Value.Year;
            if (dob.Value.Date > today.AddYears(-age)) age--;
            return age < 0 ? null : age;
        }
    }
}
