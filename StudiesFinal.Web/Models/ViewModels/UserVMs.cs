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

        [Required(ErrorMessage = "Username is required")]
        [StringLength(100)]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Obligatoria al crear; al editar, vacía = no se cambia.</summary>
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Minimum 8 characters")]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm password")]
        public string? ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Role")]
        public UserRole Role { get; set; } = UserRole.Technician;

        [Display(Name = "Active")]
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
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Enter your current password")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the new password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Minimum 8 characters")]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
