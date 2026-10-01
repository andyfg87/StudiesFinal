using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Entities;

namespace StudiesFinal.Web.Services
{
    /// <summary>
    /// Aplica las migraciones pendientes y, si no hay ningún usuario, crea el
    /// administrador inicial con los datos de la sección "SeedAdmin".
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            // Crea la base si no existe y aplica las migraciones pendientes
            await context.Database.MigrateAsync();

            if (await context.Users.AnyAsync())
                return;

            var section = configuration.GetSection("SeedAdmin");
            var username = section["Username"];
            var password = section["Password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("There are no users and the SeedAdmin section is missing: the initial administrator cannot be created.");
                return;
            }

            var admin = new User
            {
                Username = username,
                FirstName = section["FirstName"] ?? "Admin",
                LastName = section["LastName"] ?? "",
                Role = UserRole.Admin
            };
            admin.PasswordHash = hasher.HashPassword(admin, password);

            context.Users.Add(admin);
            await context.SaveChangesAsync();

            logger.LogWarning("Initial administrator '{User}' created. Change its password after the first sign-in.", username);
        }
    }
}
