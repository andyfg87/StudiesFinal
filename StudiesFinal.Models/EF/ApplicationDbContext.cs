using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;

namespace StudiesFinal.Models.EF
{
    /// <summary>
    /// Contexto de la aplicación (SQL Server: LocalDB en desarrollo, SQL Server en el servidor).
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Study> Studies { get; set; }
        public DbSet<StudyTemplate> StudyTemplates { get; set; }
        public DbSet<ApplicationLog> ApplicationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración de USER
            modelBuilder.Entity<User>(entity =>
            {
                // Único y, con la collation por defecto de SQL Server, sin distinguir mayúsculas
                entity.HasIndex(u => u.Username).IsUnique();
                entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
                entity.Ignore(u => u.FullName);
            });

            // Configuración de PATIENT (mismos índices que en Access)
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasIndex(p => p.Name);
                entity.HasIndex(p => p.DateOfBirth);
            });

            // Configuración de STUDY
            modelBuilder.Entity<Study>(entity =>
            {
                entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
                entity.Ignore(s => s.IsLocked);

                entity.HasIndex(s => s.Status);
                entity.HasIndex(s => s.StudyDate);

                entity.HasOne(s => s.Patient)
                    .WithMany(p => p.Studies)
                    .HasForeignKey(s => s.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.SignedBy)
                    .WithMany()
                    .HasForeignKey(s => s.SignedById)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ApplicationLog>(entity =>
            {
                entity.HasIndex(l => l.Timestamp);
            });
        }
    }
}
