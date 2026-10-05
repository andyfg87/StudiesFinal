using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Encodings.Web;

namespace StudiesFinal.Web.Extensions
{
    /// <summary>
    /// Añade a todo &lt;form method="post"&gt; los filtros del listado (f_*) que trae la petición,
    /// como campos ocultos. Así, después de guardar, firmar, etc. la redirección los conserva y
    /// el botón Back vuelve al Index tal como estaba. Ver <see cref="ListRoute"/>.
    /// </summary>
    [HtmlTargetElement("form", Attributes = "method")]
    public class ListFiltersFormTagHelper : TagHelper
    {
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = default!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var method = output.Attributes["method"]?.Value?.ToString();
            if (!string.Equals(method, "post", StringComparison.OrdinalIgnoreCase))
                return;

            var enc = HtmlEncoder.Default;
            foreach (var (key, value) in ListRoute.Prefixed(ViewContext.HttpContext.Request))
                output.PostContent.AppendHtml($"<input type=\"hidden\" name=\"{enc.Encode(key)}\" value=\"{enc.Encode(value)}\" />");
        }
    }
}
