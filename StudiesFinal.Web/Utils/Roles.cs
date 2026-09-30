using StudiesFinal.Models.Entities;

namespace StudiesFinal.Web.Utils
{
    /// <summary>Cadenas de roles para [Authorize(Roles = ...)].</summary>
    public static class Roles
    {
        public const string Admin = nameof(UserRole.Admin);
        public const string Doctor = nameof(UserRole.Doctor);
        public const string Technician = nameof(UserRole.Technician);

        public const string AdminOrDoctor = Admin + "," + Doctor;
        public const string All = Admin + "," + Doctor + "," + Technician;
    }
}
