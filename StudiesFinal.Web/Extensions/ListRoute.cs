using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using System.Text.Encodings.Web;

namespace StudiesFinal.Web.Extensions
{
    /// <summary>
    /// Mantiene los filtros de un listado (Index) al ir a Create/Edit/Details y volver.
    ///
    /// El Index guarda sus filtros en ViewBag.RouteValues (search, status, sortBy, pageNumber…).
    /// Los enlaces a las páginas hijas los llevan con el prefijo "f_" (f_search, f_status…) para que
    /// nunca se mezclen con los campos del formulario ni con el model binding. Las páginas hijas los
    /// reenvían (enlaces, redirecciones y, con ListFiltersFormTagHelper, cualquier form POST) y el
    /// botón Back los devuelve al Index sin el prefijo.
    /// </summary>
    public static class ListRoute
    {
        public const string Prefix = "f_";

        /// <summary>Filtros del listado que trae la petición actual (query o form), sin el prefijo.</summary>
        public static RouteValueDictionary ListFilters(this HttpRequest request)
        {
            var rv = new RouteValueDictionary();
            foreach (var (key, value) in Prefixed(request))
                rv[key[Prefix.Length..]] = value;
            return rv;
        }

        /// <summary>
        /// Valores de ruta para una página hija: <paramref name="values"/> (p. ej. new { key = 5 }) más
        /// los filtros con prefijo. Sin <paramref name="listFilters"/> se reenvían los de la petición
        /// actual; desde un Index se pasa ViewBag.RouteValues.
        /// </summary>
        public static RouteValueDictionary WithListFilters(this HttpRequest request, object? values = null,
            RouteValueDictionary? listFilters = null)
        {
            var rv = new RouteValueDictionary(values);
            if (listFilters == null)
            {
                foreach (var (key, value) in Prefixed(request))
                    rv[key] = value;
            }
            else
            {
                foreach (var (key, value) in listFilters)
                {
                    var text = value?.ToString();
                    if (!string.IsNullOrEmpty(text))
                        rv[Prefix + key] = text;
                }
            }
            return rv;
        }

        /// <summary>Pares f_* de la petición (query y, en un POST, form). Solo los no vacíos.</summary>
        public static IEnumerable<KeyValuePair<string, string>> Prefixed(HttpRequest request)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in request.Query)
                if (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && key.Length > Prefix.Length
                    && !string.IsNullOrEmpty(value.ToString()) && seen.Add(key))
                    yield return new(key, value.ToString());

            if (request.HasFormContentType)
                foreach (var (key, value) in request.Form)
                    if (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && key.Length > Prefix.Length
                        && !string.IsNullOrEmpty(value.ToString()) && seen.Add(key))
                        yield return new(key, value.ToString());
        }

        // ---- Para las vistas -------------------------------------------------

        /// <summary>URL del listado con sus filtros. <paramref name="fallback"/> se usa si no llegó ninguno.</summary>
        public static string BackToList(this IUrlHelper url, object? fallback = null, string action = "Index")
        {
            var filters = url.ActionContext.HttpContext.Request.ListFilters();
            return url.Action(action, filters.Count > 0 ? filters : new RouteValueDictionary(fallback)) ?? "/";
        }

        /// <summary>Botón "Back" al listado con sus filtros. Uso: @Html.BackButton() o @Html.BackButton(new { status = "ToSign" }).</summary>
        public static IHtmlContent BackButton(this IHtmlHelper html, object? fallback = null, string cssClass = "btn btn-outline-secondary")
        {
            var url = html.ViewContext.HttpContext.RequestServices
                .GetRequiredService<IUrlHelperFactory>()
                .GetUrlHelper(html.ViewContext);

            var enc = HtmlEncoder.Default;
            return new HtmlString(
                $"<a href=\"{enc.Encode(url.BackToList(fallback))}\" class=\"{enc.Encode(cssClass)}\">" +
                "<i class=\"bi bi-arrow-left me-1\" aria-hidden=\"true\"></i>Back</a>");
        }

        /// <summary>URL de una página hija del mismo listado, llevando los filtros.</summary>
        public static string ListChild(this IUrlHelper url, string action, object? values = null,
            RouteValueDictionary? listFilters = null)
            => url.Action(action, url.ActionContext.HttpContext.Request.WithListFilters(values, listFilters)) ?? "#";
    }
}
