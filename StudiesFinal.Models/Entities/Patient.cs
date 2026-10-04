using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudiesFinal.Models.Entities
{
    /// <summary>
    /// Patienttbl en Access. El PatientID no era autonumérico en Access, así que
    /// se conserva el mismo número y se asigna al crear (se propone el siguiente libre).
    /// </summary>
    public class Patient : IEntity<int>, ISoftDelete
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public ICollection<Study> Studies { get; set; } = new List<Study>();

        // ---- Borrado lógico (ISoftDelete) ----------------------------------------
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [StringLength(200)]
        public string? DeletedByName { get; set; }
    }
}
