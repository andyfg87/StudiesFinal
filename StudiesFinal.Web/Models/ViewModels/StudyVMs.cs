using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class StudyDisplayVM : IEntityDisplayModel<Study, int>
    {
        public int Id { get; set; }

        [Display(Name = "Date")]
        public DateTime StudyDate { get; set; }

        [Display(Name = "Study")]
        public string? StudyName { get; set; }

        [Display(Name = "Report")]
        public string? Information { get; set; }

        public int PatientId { get; set; }

        /// <summary>"Nombre Apellido".</summary>
        [Display(Name = "Patient")]
        public string? PatientName { get; set; }

        /// <summary>"Apellido, Nombre" (informe impreso / PDF).</summary>
        public string? PatientReportName { get; set; }

        public DateTime? PatientDateOfBirth { get; set; }

        public int? PatientAge => ListUi.Age(PatientDateOfBirth, StudyDate);

        [Display(Name = "Status")]
        public StudyStatus Status { get; set; }

        [Display(Name = "Processed")]
        public bool Processed { get; set; }

        public string? LinkFile1 { get; set; }
        public string? LinkFile2 { get; set; }
        public string? LinkFile3 { get; set; }

        [Display(Name = "Signed")]
        public DateTime? SignedAt { get; set; }

        [Display(Name = "Signed by")]
        public string? SignedByName { get; set; }

        /// <summary>null en las firmas importadas de Access.</summary>
        public Guid? SignedById { get; set; }

        /// <summary>PDF del informe firmado guardado en el servidor.</summary>
        public string? SignedPdfPath { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedByName { get; set; }

        public IEnumerable<(int Slot, string Path)> Links
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LinkFile1)) yield return (1, LinkFile1);
                if (!string.IsNullOrWhiteSpace(LinkFile2)) yield return (2, LinkFile2);
                if (!string.IsNullOrWhiteSpace(LinkFile3)) yield return (3, LinkFile3);
            }
        }

        public void Import(Study entity)
        {
            Id = entity.Id;
            StudyDate = entity.StudyDate;
            StudyName = entity.StudyName;
            Information = entity.Information;
            PatientId = entity.PatientId;
            PatientName = entity.Patient?.FullName;
            PatientReportName = entity.Patient?.ReportName;
            PatientDateOfBirth = entity.Patient?.DateOfBirth;
            Status = entity.Status;
            Processed = entity.Processed;
            LinkFile1 = entity.LinkFile1;
            LinkFile2 = entity.LinkFile2;
            LinkFile3 = entity.LinkFile3;
            SignedAt = entity.SignedAt;
            SignedByName = entity.SignedBy?.FullName ?? entity.SignedByName;
            SignedById = entity.SignedById;
            SignedPdfPath = entity.SignedPdfPath;
            CreatedAt = entity.CreatedAt;
            CreatedByName = entity.CreatedByName;
            UpdatedAt = entity.UpdatedAt;
            UpdatedByName = entity.UpdatedByName;
        }
    }

    public class StudyInputVM : IEntityInputModel<Study, int>
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Study date")]
        public DateTime StudyDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Select a patient")]
        [Range(0, int.MaxValue, ErrorMessage = "Select a patient")]
        [Display(Name = "Patient")]
        public int? PatientId { get; set; }

        /// <summary>Solo para mostrar el paciente elegido en el formulario.</summary>
        public string? PatientLabel { get; set; }

        /// <summary>Texto del selector de paciente: "Nº · Nombre Apellido".</summary>
        public static string? LabelFor(Patient? patient)
            => patient == null ? null : $"{patient.Id} · {patient.FullName}";

        [Display(Name = "Template")]
        public int? TemplateId { get; set; }

        [Required(ErrorMessage = "Study name is required")]
        [StringLength(50)]
        [Display(Name = "Study")]
        public string? StudyName { get; set; }

        [Display(Name = "Report")]
        public string? Information { get; set; }

        [Display(Name = "Processed")]
        public bool Processed { get; set; }

        // Rutas de los archivos del estudio. NUNCA se toman del formulario (BindNever): las rellena
        // el controlador con lo guardado + lo elegido en el explorador (FileSelectionN).
        [BindNever]
        public string? LinkFile1 { get; set; }

        [BindNever]
        public string? LinkFile2 { get; set; }

        [BindNever]
        public string? LinkFile3 { get; set; }

        /// <summary>Valor de FileSelectionN para quitar el archivo de ese hueco.</summary>
        public const string RemoveFile = "-";

        // Lo elegido en el explorador de archivos, por hueco:
        // vacío = sin cambios, "-" = quitar, ruta RELATIVA a la carpeta de estudios = archivo nuevo.
        public string? FileSelection1 { get; set; }
        public string? FileSelection2 { get; set; }
        public string? FileSelection3 { get; set; }

        public string? LinkFile(int slot) => slot switch { 1 => LinkFile1, 2 => LinkFile2, _ => LinkFile3 };

        public string? FileSelection(int slot) => slot switch { 1 => FileSelection1, 2 => FileSelection2, _ => FileSelection3 };

        /// <summary>true si en este hueco se eligió un archivo nuevo que todavía no está guardado.</summary>
        public bool HasNewSelection(int slot)
        {
            var s = FileSelection(slot);
            return !string.IsNullOrWhiteSpace(s) && s != RemoveFile;
        }

        /// <summary>Solo el nombre del archivo del hueco (la ruta no se muestra).</summary>
        public string? FileName(int slot)
        {
            var path = LinkFile(slot);
            if (string.IsNullOrWhiteSpace(path)) return null;
            var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(name) ? path : name;
        }

        /// <summary>Estado actual (solo lectura en el formulario).</summary>
        public StudyStatus Status { get; set; } = StudyStatus.InProgress;

        public SelectList? Templates { get; set; }

        /// <summary>
        /// Archivos ya guardados del estudio, para abrirlos desde el formulario.
        /// Se rellena en Import (valores guardados), no con lo que se escribe en el formulario.
        /// </summary>
        public List<StudyFileLink> SavedFiles { get; set; } = new();

        public Study Export()
        {
            var entity = new Study { Status = StudyStatus.InProgress };
            Merge(entity);
            return entity;
        }

        public void Import(Study entity)
        {
            Id = entity.Id;
            StudyDate = entity.StudyDate;
            PatientId = entity.PatientId;
            PatientLabel = LabelFor(entity.Patient);
            StudyName = entity.StudyName;
            Information = entity.Information;
            Processed = entity.Processed;
            LinkFile1 = entity.LinkFile1;
            LinkFile2 = entity.LinkFile2;
            LinkFile3 = entity.LinkFile3;
            Status = entity.Status;

            SavedFiles = new[] { (1, entity.LinkFile1), (2, entity.LinkFile2), (3, entity.LinkFile3) }
                .Where(f => !string.IsNullOrWhiteSpace(f.Item2))
                .Select(f => new StudyFileLink(entity.Id, f.Item1, f.Item2!))
                .ToList();
        }

        /// <summary>No toca el estado ni la firma: eso solo lo cambia el flujo de trabajo.</summary>
        public void Merge(Study entity)
        {
            entity.StudyDate = StudyDate.Date;
            entity.PatientId = PatientId ?? 0;
            entity.StudyName = StudyName?.Trim();
            entity.Information = Information;
            entity.Processed = Processed;
            entity.LinkFile1 = string.IsNullOrWhiteSpace(LinkFile1) ? null : LinkFile1.Trim();
            entity.LinkFile2 = string.IsNullOrWhiteSpace(LinkFile2) ? null : LinkFile2.Trim();
            entity.LinkFile3 = string.IsNullOrWhiteSpace(LinkFile3) ? null : LinkFile3.Trim();
        }
    }

    /// <summary>Un archivo del estudio con sus acciones (Views/Studies/_StudyFileLink.cshtml).</summary>
    /// <param name="Slot">1-3 = LinkFile1-3; 0 = PDF del informe firmado.</param>
    public record StudyFileLink(int StudyId, int Slot, string Path);

    /// <summary>Totales por fase para el inicio y las pestañas del listado.</summary>
    public class StudyCounts
    {
        public int InProgress { get; set; }
        public int ToSign { get; set; }
        public int Completed { get; set; }
        public int Today { get; set; }
        public int Patients { get; set; }

        public int For(StudyStatus status) => status switch
        {
            StudyStatus.InProgress => InProgress,
            StudyStatus.ToSign => ToSign,
            _ => Completed
        };
    }
}
