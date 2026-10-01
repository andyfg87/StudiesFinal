using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class StudyTemplateDisplayVM : IEntityDisplayModel<StudyTemplate, int>
    {
        public int Id { get; set; }

        [Display(Name = "Title")]
        public string StudyTitle { get; set; } = string.Empty;

        [Display(Name = "Study name")]
        public string? GenericName { get; set; }

        [Display(Name = "Content")]
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

        [Required(ErrorMessage = "Title is required")]
        [StringLength(50)]
        [Display(Name = "Template title")]
        public string StudyTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Study name is required")]
        [StringLength(50)]
        [Display(Name = "Study name (used in the report)")]
        public string? GenericName { get; set; }

        [Display(Name = "Content")]
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
