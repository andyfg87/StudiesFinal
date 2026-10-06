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
        [StringLength(250)]
        public string? LastName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public ICollection<Study> Studies { get; set; } = new List<Study>();

        // ---- Borrado lógico (ISoftDelete) ----------------------------------------
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [StringLength(200)]
        public string? DeletedByName { get; set; }

        // ---- Nombre para mostrar (solo lectura: EF no los mapea) -------------------

        /// <summary>"Nombre Apellido": cómo se muestra el paciente en toda la aplicación.</summary>
        public string FullName => FormatFull(Name, LastName);

        /// <summary>"Apellido, Nombre": cómo se muestra en el informe (PDF e impresión).</summary>
        public string ReportName => FormatReport(Name, LastName);

        public static string FormatFull(string? name, string? lastName)
            => $"{name?.Trim()} {lastName?.Trim()}".Trim();

        public static string FormatReport(string? name, string? lastName)
        {
            name = name?.Trim();
            lastName = lastName?.Trim();
            // Pacientes importados de Access: el nombre completo está en Name y no tienen apellido
            if (string.IsNullOrEmpty(lastName)) return name ?? "";
            if (string.IsNullOrEmpty(name)) return lastName;
            return $"{lastName}, {name}";
        }
    }
}
