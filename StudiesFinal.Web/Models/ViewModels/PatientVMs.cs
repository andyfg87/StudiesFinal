using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class PatientDisplayVM : IEntityDisplayModel<Patient, int>
    {
        [Display(Name = "Nº paciente")]
        public int Id { get; set; }

        [Display(Name = "Nombre")]
        public string? Name { get; set; }

        [Display(Name = "Fecha de nacimiento")]
        public DateTime? DateOfBirth { get; set; }

        public int? Age => ListUi.Age(DateOfBirth);

        [Display(Name = "Estudios")]
        public int StudyCount { get; set; }

        public DateTime? LastStudyDate { get; set; }

        public void Import(Patient entity)
        {
            Id = entity.Id;
            Name = entity.Name;
            DateOfBirth = entity.DateOfBirth;
            StudyCount = entity.Studies?.Count ?? 0;
            LastStudyDate = entity.Studies?.Count > 0 ? entity.Studies.Max(s => s.StudyDate) : null;
        }
    }

    public class PatientInputVM : IEntityInputModel<Patient, int>
    {
        /// <summary>Nº de paciente (PatientID de Access). Se propone el siguiente libre.</summary>
        [Required(ErrorMessage = "El número de paciente es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Número de paciente no válido")]
        [Display(Name = "Nº paciente")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string? Name { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de nacimiento")]
        public DateTime? DateOfBirth { get; set; }

        /// <summary>true en edición: el Nº de paciente no se puede cambiar.</summary>
        public bool IsEdit { get; set; }

        public Patient Export()
        {
            var entity = new Patient { Id = Id };
            Merge(entity);
            return entity;
        }

        public void Import(Patient entity)
        {
            Id = entity.Id;
            Name = entity.Name;
            DateOfBirth = entity.DateOfBirth;
            IsEdit = true;
        }

        public void Merge(Patient entity)
        {
            entity.Name = Name?.Trim();
            entity.DateOfBirth = DateOfBirth?.Date;
        }
    }
}
