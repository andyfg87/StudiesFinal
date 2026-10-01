using AngleSharp.Dom;
using IElement = AngleSharp.Dom.IElement;
using AngleSharp.Html.Parser;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StudiesFinal.Models.Entities;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StudiesFinal.Web.Services
{
    /// <summary>
    /// Genera el PDF del informe con el mismo formato que Views/Studies/Print.cshtml
    /// (plantilla HR-*.pdf de Access) y lo guarda al firmar en
    /// \\{StudyFiles:Server}\{StudyFiles:Share}\&lt;tipo de reporte&gt;\HR-Paciente-fecha.pdf
    /// </summary>
    public interface IReportPdfService
    {
        /// <summary>Nombre que aparece en "Electronically signed by".</summary>
        string SignerName(Study study);

        /// <summary>PDF del informe. El estudio debe traer cargado Patient.</summary>
        byte[] Generate(Study study);

        /// <summary>Genera el PDF y lo guarda en la carpeta del reporte. Devuelve la ruta.</summary>
        Task<string> GenerateAndSaveAsync(Study study);
    }

    public class ReportPdfService : IReportPdfService
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
        // Lato viene incluida en QuestPDF: es el último recurso si el servidor no tiene Calibri
        private static readonly string[] Fonts = { "Calibri", "Arial", "Lato" };

        /// <summary>
        /// QuestPDF no usa las fuentes del sistema: registra Calibri (la del informe de Access)
        /// y Arial desde la carpeta de fuentes de Windows, si existen.
        /// </summary>
        public static void RegisterFonts()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            foreach (var pattern in new[] { "calibri*.ttf", "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf" })
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern))
                {
                    try
                    {
                        using var stream = File.OpenRead(file);
                        QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
                    }
                    catch { /* fuente dañada o sin permiso: se usa la siguiente */ }
                }
            }
        }
        private const string Gray = "#7F7F7F";

        private readonly IConfigurationSection _report;
        private readonly IWebHostEnvironment _env;
        private readonly IStudyFileService _files;
        private readonly IRichTextSanitizer _sanitizer;

        public ReportPdfService(IConfiguration config, IWebHostEnvironment env, IStudyFileService files, IRichTextSanitizer sanitizer)
        {
            _report = config.GetSection("Report");
            _env = env;
            _files = files;
            _sanitizer = sanitizer;
        }

        public string SignerName(Study study)
        {
            // Las firmas importadas de Access no tienen usuario: firmó el médico por defecto
            var name = study.SignedBy?.FullName ?? study.SignedByName;
            return study.SignedById.HasValue && !string.IsNullOrWhiteSpace(name)
                ? name
                : _report["DefaultSigner"] ?? "";
        }

        public async Task<string> GenerateAndSaveAsync(Study study)
        {
            var pdf = Generate(study);
            return await _files.SaveBytesAsync(pdf, study, study.Patient, ".pdf");
        }

        public byte[] Generate(Study study)
        {
            var signed = study.Status == StudyStatus.Completed;
            var paragraphs = HtmlToParagraphs(_sanitizer.Sanitize(study.Information));
            var logo = LogoBytes();

            return QuestPDF.Fluent.Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.MarginLeft(28);
                    page.MarginRight(28);
                    page.MarginTop(26);
                    // Deja la firma a la altura de la plantilla (~100 pt sobre el borde inferior)
                    page.MarginBottom(106);
                    page.DefaultTextStyle(t => t.FontFamily(Fonts).FontSize(11).FontColor(Colors.Black));

                    // ---- Cabecera: logo + médico --------------------------------
                    page.Header().Row(row =>
                    {
                        row.ConstantItem(106).PaddingLeft(13).Height(92).AlignMiddle().Element(e =>
                        {
                            if (logo != null) e.Image(logo).FitArea();
                        });

                        // PaddingRight = ancho del logo: así el texto queda centrado en la página
                        row.RelativeItem().PaddingTop(2).PaddingRight(106).Column(col =>
                        {
                            col.Item().AlignCenter().Text(_report["PhysicianName"] ?? "").Bold().FontSize(14);
                            col.Item().PaddingTop(4).AlignCenter().Text(_report["HeaderTitle"] ?? "").Bold().FontSize(14);
                            col.Item().PaddingTop(3).AlignCenter().Text(_report["Address"] ?? "").FontSize(12);
                            col.Item().PaddingTop(3).AlignCenter().Text($"Phone: {_report["Phone"]}      Fax: {_report["Fax"]}").FontSize(12);
                        });
                    });

                    // ---- Paciente + informe ------------------------------------------
                    page.Content().PaddingTop(12).Column(col =>
                    {
                        void Field(string label, string? value, float top) =>
                            col.Item().PaddingTop(top).Row(r =>
                            {
                                r.ConstantItem(63).Text(label).Bold();
                                r.RelativeItem().Text(value ?? "").Bold();
                            });

                        Field("Patient:", study.Patient?.Name, 0);
                        Field("D.O.B:", study.Patient?.DateOfBirth?.ToString("MMM-dd-yy", Us), 12);
                        Field("Study Date:", study.StudyDate.ToString("MM/dd/yyyy", Us), 12);
                        Field("Test:", study.StudyName, 9);

                        col.Item().PaddingTop(10).Column(report =>
                        {
                            foreach (var p in paragraphs)
                                report.Item().Element(e => RenderParagraph(e, p));
                        });
                    });

                    // ---- Firma (misma posición que en la plantilla) --------------------
                    page.Footer().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(350).Column(left =>
                            {
                                left.Item().PaddingLeft(3).PaddingBottom(3)
                                    .Text(signed ? $"Electronically signed by: {SignerName(study)}" : "Not signed")
                                    .Italic().FontColor(signed ? Gray : "#B00000");
                                left.Item().Width(228).LineHorizontal(0.75f).LineColor("#D0D0D0");
                                left.Item().PaddingTop(4).PaddingLeft(5).Text(_report["PhysicianName"] ?? "").Bold().FontSize(14);
                                left.Item().PaddingTop(4).Text(_report["SignatureTitle"] ?? "").Bold().FontSize(14);
                            });

                            row.RelativeItem().AlignBottom().AlignRight().PaddingBottom(2)
                                .Text(signed ? "Final Report" : "Preliminary Report")
                                .Bold().FontSize(14).FontColor(signed ? Colors.Black : "#B00000");
                        });

                        col.Item().PaddingTop(16).AlignRight()
                            .Text(signed ? study.SignedAt?.ToString("MM/dd/yyyy h:mm:ss tt", Us) ?? "" : "")
                            .Italic().FontColor(Gray);
                    });
                });
            }).GeneratePdf();
        }

        private byte[]? LogoBytes()
        {
            var logo = (_report["Logo"] ?? "~/img/report-logo.png").TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(_env.WebRootPath, logo);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        // =====================================================================
        //  HTML del informe -> párrafos
        //  Access guarda <div><font size="2"><strong>…</strong></font></div>;
        //  el editor (Quill) guarda <p>, <strong>, <em>, <u>, <ol><li data-list>.
        // =====================================================================

        private sealed record Run(string Text, bool Bold, bool Italic, bool Underline, float Size);

        private sealed class Paragraph
        {
            public List<Run> Runs { get; } = new();
            public string Align { get; set; } = "left";
            public string Prefix { get; set; } = "";
            public int Indent { get; set; }
            public bool IsEmpty => Runs.All(r => string.IsNullOrWhiteSpace(r.Text.Replace('\u00A0', ' ')));
        }

        private sealed record Style(bool Bold, bool Italic, bool Underline, float Size);

        private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
            { "div", "p", "li", "h1", "h2", "h3", "h4", "tr", "blockquote", "pre" };

        private static List<Paragraph> HtmlToParagraphs(string? html)
        {
            var result = new List<Paragraph> { new() };
            if (string.IsNullOrWhiteSpace(html)) return new();

            var doc = new HtmlParser().ParseDocument($"<body>{html}</body>");
            Walk(doc.Body!, new Style(false, false, false, 11), result, 0);

            // Quita el párrafo vacío que queda al principio y al final por el recorrido
            while (result.Count > 0 && result[0].Runs.Count == 0) result.RemoveAt(0);
            while (result.Count > 0 && result[^1].Runs.Count == 0) result.RemoveAt(result.Count - 1);
            return result;
        }

        private static void Walk(INode node, Style style, List<Paragraph> paras, int listDepth)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child is IText text)
                {
                    var value = Regex.Replace(text.Data, @"[ \t\r\n]+", " ");
                    var current = paras[^1];
                    if (current.Runs.Count == 0) value = value.TrimStart(' ');
                    if (value.Length > 0)
                        current.Runs.Add(new Run(value, style.Bold, style.Italic, style.Underline, style.Size));
                    continue;
                }

                if (child is not IElement el) continue;
                var tag = el.LocalName;

                if (tag == "br")
                {
                    NewParagraph(paras, force: true);
                    continue;
                }

                var s = style;
                switch (tag)
                {
                    case "b" or "strong": s = s with { Bold = true }; break;
                    case "i" or "em": s = s with { Italic = true }; break;
                    case "u": s = s with { Underline = true }; break;
                    case "h1": s = s with { Bold = true, Size = 18 }; break;
                    case "h2": s = s with { Bold = true, Size = 15 }; break;
                    case "h3" or "h4": s = s with { Bold = true, Size = 13 }; break;
                    case "font":
                        s = el.GetAttribute("size") switch
                        {
                            "1" => s with { Size = 9 },
                            "2" => s with { Size = 11 },
                            "3" => s with { Size = 12 },
                            "4" => s with { Size = 14 },
                            "5" => s with { Size = 18 },
                            "6" or "7" => s with { Size = 24 },
                            _ => s
                        };
                        break;
                }

                var styleAttr = el.GetAttribute("style") ?? "";
                if (Regex.IsMatch(styleAttr, @"font-weight\s*:\s*(bold|[6-9]00)")) s = s with { Bold = true };
                if (Regex.IsMatch(styleAttr, @"font-style\s*:\s*italic")) s = s with { Italic = true };
                if (Regex.IsMatch(styleAttr, @"text-decoration[^;]*underline")) s = s with { Underline = true };

                if (BlockTags.Contains(tag))
                {
                    NewParagraph(paras);
                    var p = paras[^1];
                    p.Align = AlignOf(el);
                    p.Indent = listDepth + IndentOf(el);

                    if (tag == "li")
                    {
                        var list = el.ParentElement;
                        var bullet = el.GetAttribute("data-list") == "bullet" || list?.LocalName == "ul";
                        p.Prefix = bullet ? "•  " : $"{el.Index() + 1}.  ";
                    }

                    Walk(el, s, paras, listDepth);
                    NewParagraph(paras);
                }
                else if (tag is "ol" or "ul")
                {
                    Walk(el, s, paras, listDepth + 1);
                }
                else
                {
                    Walk(el, s, paras, listDepth);
                }
            }
        }

        /// <summary>Abre un párrafo nuevo si el actual ya tiene texto (o siempre, con force).</summary>
        private static void NewParagraph(List<Paragraph> paras, bool force = false)
        {
            if (force || paras[^1].Runs.Count > 0)
                paras.Add(new Paragraph());
        }

        private static string AlignOf(IElement el)
        {
            var cls = el.ClassName ?? "";
            var style = el.GetAttribute("style") ?? "";
            var align = el.GetAttribute("align") ?? "";
            if (cls.Contains("ql-align-center") || style.Contains("text-align: center") || align == "center") return "center";
            if (cls.Contains("ql-align-right") || style.Contains("text-align: right") || align == "right") return "right";
            if (cls.Contains("ql-align-justify") || style.Contains("justify") || align == "justify") return "justify";
            return "left";
        }

        private static int IndentOf(IElement el)
        {
            var m = Regex.Match(el.ClassName ?? "", @"ql-indent-(\d)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static void RenderParagraph(IContainer container, Paragraph p)
        {
            container = container.PaddingLeft(p.Indent * 18);

            // Línea en blanco (<div>&nbsp;</div> de Access)
            if (p.IsEmpty)
            {
                container.Text("\u00A0");
                return;
            }

            container.Text(t =>
            {
                switch (p.Align)
                {
                    case "center": t.AlignCenter(); break;
                    case "right": t.AlignRight(); break;
                    case "justify": t.Justify(); break;
                }

                if (p.Prefix.Length > 0)
                    t.Span(p.Prefix);

                foreach (var r in p.Runs)
                {
                    var span = t.Span(r.Text).FontSize(r.Size);
                    if (r.Bold) span.Bold();
                    if (r.Italic) span.Italic();
                    if (r.Underline) span.Underline();
                }
            });
        }
    }
}
