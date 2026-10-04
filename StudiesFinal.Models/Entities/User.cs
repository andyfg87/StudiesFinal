using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Models.Entities
{
    public class User : IEntity<Guid>, ISoftDelete
    {
        public User()
        {
            Id = Guid.NewGuid();
        }

        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true;

        // ---- Borrado lógico (ISoftDelete) ----------------------------------------
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [StringLength(200)]
        public string? DeletedByName { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    public enum UserRole
    {
        Admin,
        Doctor,
        Technician
    }
}
