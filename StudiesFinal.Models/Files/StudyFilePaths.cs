using System.Text.RegularExpressions;

namespace StudiesFinal.Models.Files
{
    /// <summary>
    /// Reglas de rutas de los archivos de estudios, compartidas por la web y el importador:
    ///   raíz:     \\192.168.199.140\Fileserver\Studies
    ///   informes: \\192.168.199.140\Fileserver\Studies\Studies Report\&lt;tipo de reporte&gt;
    /// </summary>
    public class StudyFilePaths
    {
        private readonly StudyFilesOptions _options;

        public StudyFilePaths(StudyFilesOptions options)
        {
            _options = options;
            BasePath = options.ResolveBasePath();
            ReportsRoot = string.IsNullOrWhiteSpace(options.ReportsFolder)
                ? BasePath
                : Path.Combine(BasePath, options.ReportsFolder.Trim().Trim('\\', '/'));
        }

        /// <summary>\\{Server}\{Share}, p. ej. \\192.168.199.140\Fileserver\Studies</summary>
        public string BasePath { get; }

        /// <summary>BasePath\Studies Report</summary>
        public string ReportsRoot { get; }

        public IReadOnlyList<string> LegacyPrefixes => _options.LegacyPrefixes;

        /// <summary>
        /// Limpia la ruta (hipervínculo de Access "texto#dirección#", comillas, /) y
        /// reescribe los prefijos antiguos (Z:\Studies, \\192.168.199.170\Studies…) a BasePath.
        /// </summary>
        public string? Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var p = path.Trim().Trim('"').Replace('/', '\\');

            if (p.Contains('#'))
            {
                var parts = p.Split('#');
                p = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : parts[0];
            }

            foreach (var prefix in _options.LegacyPrefixes)
            {
                var legacy = prefix.TrimEnd('\\');
                if (p.StartsWith(legacy + "\\", StringComparison.OrdinalIgnoreCase))
                    return BasePath + p[legacy.Length..];
            }

            return p;
        }

        /// <summary>true si la ruta está dentro de BasePath (evita leer archivos arbitrarios).</summary>
        public bool IsAllowed(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!path.StartsWith(BasePath + "\\", StringComparison.OrdinalIgnoreCase)) return false;

            // Se rechazan los segmentos que suben de carpeta (\..\), no un nombre como "JOHN..pdf"
            var segments = path[(BasePath.Length + 1)..].Split('\\', '/');
            return segments.All(s => s != ".." && s != ".");
        }

        /// <summary>
        /// Carpeta del tipo de reporte: ReportsRoot\Holter Report
        /// (salvo que StudyFiles:Folders indique otra carpeta para ese tipo).
        /// </summary>
        public string FolderFor(string? studyName)
        {
            var name = Sanitize(studyName);
            if (string.IsNullOrEmpty(name))
                return Path.Combine(ReportsRoot, _options.DefaultFolder);

            var folder = _options.Folders.TryGetValue(name, out var custom) && !string.IsNullOrWhiteSpace(custom)
                ? custom
                : name;

            return Path.Combine(ReportsRoot, folder);
        }

        /// <summary>Iniciales del tipo de reporte: "Holter Report" -> "HR" (o StudyFiles:Prefixes).</summary>
        public string PrefixFor(string? studyName)
        {
            var name = Sanitize(studyName);
            if (string.IsNullOrEmpty(name)) return "ST";

            if (_options.Prefixes.TryGetValue(name, out var custom) && !string.IsNullOrWhiteSpace(custom))
                return custom.Trim();

            return string.Concat(Regex.Matches(name, @"\p{L}+").Select(m => char.ToUpperInvariant(m.Value[0])));
        }

        /// <summary>Quita caracteres no válidos en nombres de archivo y las comas ("Angulo, Juan" -> "Angulo Juan").</summary>
        public static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var clean = Regex.Replace(value, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))},]", " ");
            return Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('.');
        }
    }
}
