using Ganss.Xss;
using Microsoft.AspNetCore.Html;

namespace StudiesFinal.Web.Services
{
    /// <summary>
    /// Limpia el HTML de los informes (texto enriquecido de Access y del editor)
    /// antes de guardarlo y antes de pintarlo, para evitar scripts incrustados.
    /// </summary>
    public interface IRichTextSanitizer
    {
        string? Sanitize(string? html);
        IHtmlContent Render(string? html);
    }

    public class RichTextSanitizer : IRichTextSanitizer
    {
        private readonly HtmlSanitizer _sanitizer;

        public RichTextSanitizer()
        {
            _sanitizer = new HtmlSanitizer();
            // Access guarda el texto enriquecido con <font face/size/color>
            _sanitizer.AllowedTags.Add("font");
            _sanitizer.AllowedAttributes.Add("face");
            _sanitizer.AllowedAttributes.Add("size");
            _sanitizer.AllowedAttributes.Add("color");
            _sanitizer.AllowedAttributes.Add("class"); // clases de Quill (ql-align-center, ql-indent-1…)
            _sanitizer.AllowedAttributes.Add("data-list"); // listas de Quill 2
            _sanitizer.AllowedSchemes.Add("file");
        }

        public string? Sanitize(string? html)
            => string.IsNullOrWhiteSpace(html) ? null : _sanitizer.Sanitize(html);

        public IHtmlContent Render(string? html)
            => new HtmlString(Sanitize(html) ?? string.Empty);
    }
}
