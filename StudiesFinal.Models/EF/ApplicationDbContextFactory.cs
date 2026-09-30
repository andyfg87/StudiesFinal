using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudiesFinal.Models.EF
{
    /// <summary>Solo para las herramientas de diseño (dotnet ef migrations ...).</summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlite("Data Source=studiesfinal.db");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
