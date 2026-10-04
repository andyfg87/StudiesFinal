using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Models.Entities
{
    /// <summary>
    /// StudyTemplate en Access. StudyInfo es texto enriquecido (HTML).
    /// GenericName es el nombre con el que se guarda el estudio (StudyName).
    /// </summary>
    public class StudyTemplate : IEntity<int>, ISoftDelete
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string StudyTitle { get; set; } = string.Empty;

        public string? StudyInfo { get; set; }

        [StringLength(50)]
        public string? GenericName { get; set; }

        // ---- Borrado lógico (ISoftDelete) ----------------------------------------
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [StringLength(200)]
        public string? DeletedByName { get; set; }
    }
}
