using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudiesFinal.Models.EF
{
    /// <summary>
    /// Solo para las herramientas de diseño (dotnet ef migrations ...).
    /// La conexión real de la web y del importador está en appsettings.json:
    /// LocalDB en desarrollo y SERVER01\SQLEXPRESS en appsettings.Production.json.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            //optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=StudiesFinal;Integrated Security=True;TrustServerCertificate=True;");
            optionsBuilder.UseSqlServer("Server=SERVER01\\SQLEXPRESS;Database=StudiesFinal;Integrated Security=True;TrustServerCertificate=True;");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
