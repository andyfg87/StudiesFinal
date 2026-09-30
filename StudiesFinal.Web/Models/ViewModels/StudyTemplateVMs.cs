using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class StudyTemplateDisplayVM : IEntityDisplayModel<StudyTemplate, int>
    {
        public int Id { get; set; }

        [Display(Name = "Título")]
        public string StudyTitle { get; set; } = string.Empty;

        [Display(Name = "Nombre del estudio")]
        public string? GenericName { get; set; }

        [Display(Name = "Contenido")]
        public string? StudyInfo { get; set; }

        public void Import(StudyTemplate entity)
        {
            Id = entity.Id;
            StudyTitle = entity.StudyTitle;
            GenericName = entity.GenericName;
            StudyInfo = entity.StudyInfo;
        }
    }

    public class StudyTemplateInputVM : IEntityInputModel<StudyTemplate, int>
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El título es obligatorio")]
        [StringLength(50)]
        [Display(Name = "Título de la plantilla")]
        public string StudyTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del estudio es obligatorio")]
        [StringLength(50)]
        [Display(Name = "Nombre del estudio (se usa en el informe)")]
        public string? GenericName { get; set; }

        [Display(Name = "Contenido")]
        public string? StudyInfo { get; set; }

        public StudyTemplate Export()
        {
            var entity = new StudyTemplate();
            Merge(entity);
            return entity;
        }

        public void Import(StudyTemplate entity)
        {
            Id = entity.Id;
            StudyTitle = entity.StudyTitle;
            GenericName = entity.GenericName;
            StudyInfo = entity.StudyInfo;
        }

        public void Merge(StudyTemplate entity)
        {
            entity.StudyTitle = StudyTitle.Trim();
            entity.GenericName = GenericName?.Trim();
            entity.StudyInfo = StudyInfo;
        }
    }
}
