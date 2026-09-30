using Microsoft.AspNetCore.Mvc.Rendering;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Web.Models.ViewModels
{
    public class UserDisplayVM : IEntityDisplayModel<User, Guid>
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string RoleLabel => Role.RoleLabel();
        public bool IsActive { get; set; }

        public void Import(User entity)
        {
            Id = entity.Id;
            UserName = entity.Username;
            FullName = entity.FullName;
            Role = entity.Role;
            IsActive = entity.IsActive;
        }
    }

    public class UserInputVM : IEntityInputModel<User, Guid>
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "El usuario es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Usuario")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Los apellidos son obligatorios")]
        [StringLength(100)]
        [Display(Name = "Apellidos")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Obligatoria al crear; al editar, vacía = no se cambia.</summary>
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres")]
        [Display(Name = "Contraseña")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string? ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Rol")]
        public UserRole Role { get; set; } = UserRole.Technician;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;

        public bool IsEdit { get; set; }

        public SelectList? AvailableRoles { get; set; }

        public User Export()
        {
            var entity = new User();
            Merge(entity);
            return entity;
        }

        public void Import(User entity)
        {
            Id = entity.Id;
            UserName = entity.Username;
            FirstName = entity.FirstName;
            LastName = entity.LastName;
            Role = entity.Role;
            IsActive = entity.IsActive;
            IsEdit = true;
        }

        public void Merge(User entity)
        {
            entity.Username = UserName.Trim();
            entity.FirstName = FirstName.Trim();
            entity.LastName = LastName.Trim();
            entity.Role = Role;
            entity.IsActive = IsActive;
        }
    }

    public class LoginVM
    {
        [Required(ErrorMessage = "El usuario es obligatorio")]
        [Display(Name = "Usuario")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Escribe tu contraseña actual")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe la nueva contraseña")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres")]
        [Display(Name = "Nueva contraseña")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
