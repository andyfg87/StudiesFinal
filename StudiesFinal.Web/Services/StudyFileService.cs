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

        /// <summary>Guarda un archivo subido en la carpeta del estudio y devuelve la ruta completa.</summary>
        Task<string> SaveAsync(IFormFile file, Study study, Patient patient, int slot);

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
    }
}
