using Microsoft.Extensions.Options;
using StudiesFinal.Models.Entities;
using System.Text.RegularExpressions;

namespace StudiesFinal.Web.Services
{
    /// <summary>
    /// Configuración "StudyFiles" de appsettings.
    /// Los archivos de los estudios viven en la carpeta compartida \\{Server}\{Share};
    /// todos los links deben empezar por <see cref="BasePath"/>.
    /// </summary>
    public class StudyFilesOptions
    {
        public const string Section = "StudyFiles";

        /// <summary>IP o nombre del servidor de archivos (p. ej. 192.168.199.140).</summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>Carpeta compartida dentro del servidor (p. ej. Studies).</summary>
        public string Share { get; set; } = "Studies";

        /// <summary>
        /// Opcional: ruta raíz completa. Si se indica, tiene prioridad sobre Server/Share
        /// (útil para pruebas con una carpeta local).
        /// </summary>
        public string? BasePath { get; set; }

        /// <summary>Raíz efectiva: BasePath o \\{Server}\{Share}.</summary>
        public string ResolveBasePath()
        {
            if (!string.IsNullOrWhiteSpace(BasePath))
                return BasePath.Trim().TrimEnd('\\', '/');

            if (string.IsNullOrWhiteSpace(Server))
                throw new InvalidOperationException(
                    "StudyFiles:Server is missing in appsettings.json (IP of the studies file server).");

            var server = Server.Trim().TrimStart('\\').TrimEnd('\\');
            var share = (Share ?? "").Trim().Trim('\\', '/');
            return string.IsNullOrEmpty(share) ? $@"\\{server}" : $@"\\{server}\{share}";
        }

        /// <summary>
        /// Prefijos antiguos (unidades mapeadas / servidores anteriores) que se reescriben
        /// a la raíz actual. P. ej. "Z:\Studies" o "\\192.168.199.170\Studies".
        /// </summary>
        public List<string> LegacyPrefixes { get; set; } = new();

        /// <summary>
        /// Excepciones de carpeta por tipo de reporte. Por defecto la carpeta es el propio
        /// nombre del reporte: \\{Server}\{Share}\Holter Report
        /// </summary>
        public Dictionary<string, string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Excepciones del prefijo del nombre de archivo. Por defecto son las iniciales
        /// del reporte: "Holter Report" -> HR.
        /// </summary>
        public Dictionary<string, string> Prefixes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Carpeta para estudios sin tipo de reporte.</summary>
        public string DefaultFolder { get; set; } = "Other";

        public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;
    }

    public interface IStudyFileService
    {
        string BasePath { get; }

        /// <summary>Reescribe prefijos antiguos al servidor actual.</summary>
        string? Normalize(string? path);

        /// <summary>true si la ruta está dentro de BasePath (evita leer archivos arbitrarios).</summary>
        bool IsAllowed(string? path);

        /// <summary>Carpeta completa donde se guardan los archivos de ese tipo de estudio.</summary>
        string FolderFor(string? studyName);

        /// <summary>Iniciales del tipo de reporte para el nombre del archivo ("HR").</summary>
        string PrefixFor(string? studyName);

        /// <summary>Nombre con el que se guarda: HR-Paciente-MM-dd-yyyy.ext (fecha de hoy).</summary>
        string FileNameFor(string? studyName, string? patientName, string extension);

        /// <summary>Guarda un archivo subido en la carpeta del estudio y devuelve la ruta completa.</summary>
        Task<string> SaveAsync(IFormFile file, Study study, Patient patient, int slot);

        /// <summary>Guarda contenido generado (p. ej. el PDF del informe firmado) y devuelve la ruta completa.</summary>
        Task<string> SaveBytesAsync(byte[] content, Study study, Patient? patient, string extension);
    }

    public class StudyFileService : IStudyFileService
    {
        private readonly StudyFilesOptions _options;

        public StudyFileService(IOptions<StudyFilesOptions> options)
        {
            _options = options.Value;
            BasePath = _options.ResolveBasePath();
        }

        /// <summary>\\{Server}\{Share}, p. ej. \\192.168.199.140\Studies</summary>
        public string BasePath { get; }

        public string? Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var p = path.Trim().Trim('"').Replace('/', '\\');

            // Los hipervínculos de Access vienen como "texto#dirección#": nos quedamos con la dirección
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

        public bool IsAllowed(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.Contains("..")) return false;

            return path.StartsWith(BasePath + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Carpeta = tipo de reporte: \\{Server}\{Share}\Holter Report
        /// (salvo que StudyFiles:Folders indique otra para ese tipo).
        /// </summary>
        public string FolderFor(string? studyName)
        {
            var name = Sanitize(studyName);
            if (string.IsNullOrEmpty(name))
                return Path.Combine(BasePath, _options.DefaultFolder);

            var folder = _options.Folders.TryGetValue(name, out var custom) && !string.IsNullOrWhiteSpace(custom)
                ? custom
                : name;

            return Path.Combine(BasePath, folder);
        }

        /// <summary>Iniciales del tipo de reporte: "Holter Report" -> "HR" (o StudyFiles:Prefixes).</summary>
        public string PrefixFor(string? studyName)
        {
            var name = Sanitize(studyName);
            if (string.IsNullOrEmpty(name)) return "ST";

            if (_options.Prefixes.TryGetValue(name, out var custom) && !string.IsNullOrWhiteSpace(custom))
                return custom.Trim();

            var initials = Regex.Matches(name, @"\p{L}+")
                .Select(m => char.ToUpperInvariant(m.Value[0]));
            return string.Concat(initials);
        }

        /// <summary>
        /// Nombre del archivo como los informes de Access: HR-Angulo Juan-09-30-2026.pdf
        /// (iniciales del reporte - paciente - fecha de hoy).
        /// </summary>
        public string FileNameFor(string? studyName, string? patientName, string extension)
        {
            var patient = Sanitize(patientName);
            if (string.IsNullOrEmpty(patient)) patient = "Patient";
            return $"{PrefixFor(studyName)}-{patient}-{DateTime.Today:MM-dd-yyyy}{extension}";
        }

        public async Task<string> SaveAsync(IFormFile file, Study study, Patient patient, int slot)
        {
            if (file.Length > _options.MaxUploadBytes)
                throw new InvalidOperationException($"The file exceeds the {_options.MaxUploadBytes / (1024 * 1024)} MB limit.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var target = NewTargetPath(study.StudyName, patient.Name, ext);

            await using var stream = new FileStream(target, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return target;
        }

        public async Task<string> SaveBytesAsync(byte[] content, Study study, Patient? patient, string extension)
        {
            var target = NewTargetPath(study.StudyName, patient?.Name, extension);
            await File.WriteAllBytesAsync(target, content);
            return target;
        }

        /// <summary>
        /// Carpeta del reporte + nombre del archivo, creando la carpeta si hace falta.
        /// Si ya existe (otro archivo del mismo día): "HR-Angulo Juan-09-30-2026 (2).pdf"
        /// </summary>
        private string NewTargetPath(string? studyName, string? patientName, string ext)
        {
            var folder = FolderFor(studyName);
            Directory.CreateDirectory(folder);

            var fileName = FileNameFor(studyName, patientName, ext);
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            var target = Path.Combine(folder, fileName);
            for (var i = 2; File.Exists(target); i++)
                target = Path.Combine(folder, $"{baseName} ({i}){ext}");

            return target;
        }

        /// <summary>Quita caracteres no válidos en nombres de archivo y las comas ("Angulo, Juan" -> "Angulo Juan").</summary>
        private static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var clean = Regex.Replace(value, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))},]", " ");
            return Regex.Replace(clean, @"\s+", " ").Trim().TrimEnd('.');
        }
    }
}
