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

            // Ubicaciones del explorador (StudyFiles:BrowseRoots). Si la raíz de estudios no está
            // dentro de ninguna, se añade como "Studies" para que siempre se pueda abrir.
            var roots = new List<BrowseLocation>();
            foreach (var r in options.BrowseRoots)
            {
                var path = CleanRoot(r.Path);
                var name = Sanitize(r.Name);
                if (path.Length == 0 || name.Length == 0) continue;
                if (roots.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                roots.Add(new BrowseLocation(name, path));
            }
            if (!roots.Any(r => IsUnder(BasePath, r.Path)))
                roots.Add(new BrowseLocation(UniqueName(roots, "Studies"), BasePath));

            Locations = roots;
        }

        /// <summary>\\{Server}\{Share}, p. ej. \\192.168.199.140\Fileserver\Studies</summary>
        public string BasePath { get; }

        /// <summary>BasePath\Studies Report</summary>
        public string ReportsRoot { get; }

        /// <summary>Ubicaciones que se pueden recorrer y abrir (nombre visible + ruta).</summary>
        public IReadOnlyList<BrowseLocation> Locations { get; }

        public record BrowseLocation(string Name, string Path);

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

        /// <summary>
        /// true si la ruta está dentro de una de las ubicaciones permitidas (Locations), sin
        /// segmentos que suban de carpeta. Evita leer archivos arbitrarios del servidor.
        /// </summary>
        public bool IsAllowed(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var p = path.Replace('/', '\\');

            var root = Locations.FirstOrDefault(r => IsUnder(p, r.Path) && p.Length > r.Path.Length);
            if (root == null) return false;

            // Se rechazan los segmentos que suben de carpeta (\..\), no un nombre como "JOHN..pdf"
            var segments = p[(root.Path.Length + 1)..].Split('\\');
            return segments.All(s => s != ".." && s != ".");
        }

        /// <summary>
        /// Ruta del explorador ("Fileserver\Studies\Stress Test Treadmill\JOSE.pdf": nombre de la
        /// ubicación + ruta dentro de ella) -> ruta completa. null si no es válida o sale de la
        /// ubicación. Es lo único que acepta el explorador de archivos de la web.
        /// </summary>
        public string? Resolve(string? browserPath)
        {
            if (string.IsNullOrWhiteSpace(browserPath)) return null;

            var rel = browserPath.Trim().Replace('/', '\\').Trim('\\');
            if (rel.Length == 0 || rel.Contains(':')) return null; // absoluta / otra unidad

            var parts = rel.Split('\\', 2);
            var root = Locations.FirstOrDefault(r => r.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
            if (root == null) return null;
            if (parts.Length == 1) return root.Path;

            var full = root.Path + "\\" + parts[1];
            return IsAllowed(full) ? full : null;
        }

        /// <summary>Ruta completa -> ruta del explorador ("Ubicación\carpeta\archivo"), o null.</summary>
        public string? ToBrowserPath(string? fullPath)
        {
            var p = Normalize(fullPath)?.TrimEnd('\\');
            if (p == null) return null;

            // La ubicación más concreta que la contiene
            var root = Locations.Where(r => IsUnder(p, r.Path)).OrderByDescending(r => r.Path.Length).FirstOrDefault();
            if (root == null) return null;
            if (p.Length == root.Path.Length) return root.Name;
            return IsAllowed(p) ? root.Name + "\\" + p[(root.Path.Length + 1)..] : null;
        }

        /// <summary>true si la ruta es la ubicación o está dentro de ella.</summary>
        private static bool IsUnder(string path, string root)
            => path.Equals(root, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);

        /// <summary>"\\server\share\" -> "\\server\share"; "D:\" -> "D:" (luego se le añade "\x").</summary>
        private static string CleanRoot(string? path)
            => (path ?? "").Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');

        private static string UniqueName(List<BrowseLocation> roots, string name)
        {
            var candidate = name;
            for (var i = 2; roots.Any(r => r.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); i++)
                candidate = $"{name} {i}";
            return candidate;
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
