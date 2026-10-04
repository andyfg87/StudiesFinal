using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;

namespace StudiesFinal.Models.EF
{
    /// <summary>
    /// Contexto de la aplicación (SQL Server: LocalDB en desarrollo, SQL Server en el servidor).
    /// Estudios, pacientes, plantillas y usuarios tienen borrado lógico (ISoftDelete): las filas
    /// eliminadas se ocultan en todas las consultas y un Remove se guarda como marca.
    /// Para verlas: IgnoreQueryFilters().
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
                // Único entre los usuarios no eliminados (un usuario eliminado no bloquea su nombre);
                // con la collation por defecto de SQL Server, sin distinguir mayúsculas
                entity.HasIndex(u => u.Username).IsUnique().HasFilter("[IsDeleted] = 0");
                entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
                entity.Ignore(u => u.FullName);
                entity.HasQueryFilter(u => !u.IsDeleted);
            });

            // Configuración de PATIENT (mismos índices que en Access)
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasIndex(p => p.Name);
                entity.HasIndex(p => p.DateOfBirth);
                entity.HasQueryFilter(p => !p.IsDeleted);
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

                entity.HasQueryFilter(s => !s.IsDeleted);
            });

            modelBuilder.Entity<StudyTemplate>(entity =>
            {
                entity.HasQueryFilter(t => !t.IsDeleted);
            });

            modelBuilder.Entity<ApplicationLog>(entity =>
            {
                entity.HasIndex(l => l.Timestamp);
            });
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplySoftDelete();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplySoftDelete();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>Un Remove de una entidad ISoftDelete se guarda como "eliminado", no se borra.</summary>
        private void ApplySoftDelete()
        {
            foreach (var entry in ChangeTracker.Entries<ISoftDelete>().Where(e => e.State == EntityState.Deleted))
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt ??= DateTime.Now;
            }
        }
    }
}
