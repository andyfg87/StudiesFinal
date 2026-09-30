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

        [Display(Name = "Fecha")]
        public DateTime StudyDate { get; set; }

        [Display(Name = "Estudio")]
        public string? StudyName { get; set; }

        [Display(Name = "Informe")]
        public string? Information { get; set; }

        public int PatientId { get; set; }

        [Display(Name = "Paciente")]
        public string? PatientName { get; set; }

        public DateTime? PatientDateOfBirth { get; set; }

        public int? PatientAge => ListUi.Age(PatientDateOfBirth, StudyDate);

        [Display(Name = "Estado")]
        public StudyStatus Status { get; set; }

        [Display(Name = "Procesado")]
        public bool Processed { get; set; }

        public string? LinkFile1 { get; set; }
        public string? LinkFile2 { get; set; }
        public string? LinkFile3 { get; set; }

        [Display(Name = "Firmado")]
        public DateTime? SignedAt { get; set; }

        [Display(Name = "Firmado por")]
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
            PatientName = entity.Patient?.Name;
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

        [Required(ErrorMessage = "La fecha es obligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha del estudio")]
        public DateTime StudyDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Selecciona un paciente")]
        [Range(0, int.MaxValue, ErrorMessage = "Selecciona un paciente")]
        [Display(Name = "Paciente")]
        public int? PatientId { get; set; }

        /// <summary>Solo para mostrar el paciente elegido en el formulario.</summary>
        public string? PatientLabel { get; set; }

        [Display(Name = "Plantilla")]
        public int? TemplateId { get; set; }

        [Required(ErrorMessage = "El nombre del estudio es obligatorio")]
        [StringLength(50)]
        [Display(Name = "Estudio")]
        public string? StudyName { get; set; }

        [Display(Name = "Informe")]
        public string? Information { get; set; }

        [Display(Name = "Procesado")]
        public bool Processed { get; set; }

        [StringLength(400)]
        [Display(Name = "Archivo 1")]
        public string? LinkFile1 { get; set; }

        [StringLength(400)]
        [Display(Name = "Archivo 2")]
        public string? LinkFile2 { get; set; }

        [StringLength(400)]
        [Display(Name = "Archivo 3")]
        public string? LinkFile3 { get; set; }

        // Archivos subidos desde el navegador (se guardan en el servidor de estudios)
        public IFormFile? Upload1 { get; set; }
        public IFormFile? Upload2 { get; set; }
        public IFormFile? Upload3 { get; set; }

        /// <summary>Estado actual (solo lectura en el formulario).</summary>
        public StudyStatus Status { get; set; } = StudyStatus.InProgress;

        public SelectList? Templates { get; set; }

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
            PatientLabel = entity.Patient != null ? $"{entity.Patient.Id} · {entity.Patient.Name}" : null;
            StudyName = entity.StudyName;
            Information = entity.Information;
            Processed = entity.Processed;
            LinkFile1 = entity.LinkFile1;
            LinkFile2 = entity.LinkFile2;
            LinkFile3 = entity.LinkFile3;
            Status = entity.Status;
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
