using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class PatientDisplayVM : IEntityDisplayModel<Patient, int>
    {
        [Display(Name = "Patient #")]
        public int Id { get; set; }

        [Display(Name = "Name")]
        public string? Name { get; set; }
        [Display(Name = "Last name")]
        public string? LastName { get; set; }

        /// <summary>"Nombre Apellido".</summary>
        public string FullName => Patient.FormatFull(Name, LastName);

        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        public int? Age => ListUi.Age(DateOfBirth);

        [Display(Name = "Studies")]
        public int StudyCount { get; set; }

        public DateTime? LastStudyDate { get; set; }

        public void Import(Patient entity)
        {
            Id = entity.Id;
            Name = entity.Name;
            LastName = entity.LastName;
            DateOfBirth = entity.DateOfBirth;
            StudyCount = entity.Studies?.Count ?? 0;
            LastStudyDate = entity.Studies?.Count > 0 ? entity.Studies.Max(s => s.StudyDate) : null;
        }
    }

    public class PatientInputVM : IEntityInputModel<Patient, int>
    {
        /// <summary>
        /// Nº de paciente (PatientID de Access). No se muestra en el formulario: al crear lo
        /// asigna el servidor (siguiente libre) y al editar viaja oculto.
        /// </summary>
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string? Name { get; set; }
        
        [StringLength(250)]
        [Display(Name = "Last name")]
        public string? LastName { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
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
            LastName = entity.LastName;
            DateOfBirth = entity.DateOfBirth;
            IsEdit = true;
        }

        public void Merge(Patient entity)
        {
            entity.Name = Name?.Trim();
            entity.LastName = LastName?.Trim();
            entity.DateOfBirth = DateOfBirth?.Date;
        }
    }
}
