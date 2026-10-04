using Microsoft.Extensions.Options;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Files;

namespace StudiesFinal.Web.Services
{
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

        /// <summary>Ubicaciones del explorador de archivos (StudyFiles:BrowseRoots).</summary>
        IReadOnlyList<StudyFilePaths.BrowseLocation> Locations { get; }

        /// <summary>
        /// Ruta del explorador ("Ubicación\carpeta\archivo") -> ruta completa.
        /// null si no es válida o sale de las ubicaciones permitidas.
        /// </summary>
        string? Resolve(string? browserPath);

        /// <summary>Ruta completa -> ruta del explorador (null si no está en ninguna ubicación).</summary>
        string? ToBrowserPath(string? fullPath);

        /// <summary>Guarda contenido generado (p. ej. el PDF del informe firmado) y devuelve la ruta completa.</summary>
        Task<string> SaveBytesAsync(byte[] content, Study study, Patient? patient, string extension);
    }

    /// <summary>
    /// Guarda y localiza los archivos de los estudios. Las reglas de rutas están en
    /// <see cref="StudyFilePaths"/> (proyecto Models), compartidas con el importador.
    /// </summary>
    public class StudyFileService : IStudyFileService
    {
        private readonly StudyFilesOptions _options;
        private readonly StudyFilePaths _paths;

        public StudyFileService(IOptions<StudyFilesOptions> options)
        {
            _options = options.Value;
            _paths = new StudyFilePaths(_options);
        }

        /// <summary>\\{Server}\{Share}, p. ej. \\192.168.199.140\Fileserver\Studies</summary>
        public string BasePath => _paths.BasePath;

        public string? Normalize(string? path) => _paths.Normalize(path);

        public bool IsAllowed(string? path) => _paths.IsAllowed(path);

        public string FolderFor(string? studyName) => _paths.FolderFor(studyName);

        public string PrefixFor(string? studyName) => _paths.PrefixFor(studyName);

        /// <summary>
        /// Nombre del archivo como los informes de Access: HR-Angulo Juan-09-30-2026.pdf
        /// (iniciales del reporte - paciente - fecha de hoy).
        /// </summary>
        public string FileNameFor(string? studyName, string? patientName, string extension)
        {
            var patient = StudyFilePaths.Sanitize(patientName);
            if (string.IsNullOrEmpty(patient)) patient = "Patient";
            return $"{PrefixFor(studyName)}-{patient}-{DateTime.Today:MM-dd-yyyy}{extension}";
        }

        public IReadOnlyList<StudyFilePaths.BrowseLocation> Locations => _paths.Locations;

        public string? Resolve(string? browserPath) => _paths.Resolve(browserPath);

        public string? ToBrowserPath(string? fullPath) => _paths.ToBrowserPath(fullPath);

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
    }
}
