using StudiesFinal.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Models.Entities
{
    /// <summary>
    /// StudyTBL en Access.
    /// Estado/isComplete de Access se sustituyen por <see cref="Status"/>:
    ///   isComplete = No                     -> InProgress
    ///   isComplete = Yes, Estado Not Signed -> ToSign
    ///   Estado = Signed                     -> Completed
    /// Signature (fecha como texto) pasa a SignedAt + quién firmó.
    /// </summary>
    public class Study : IEntity<int>, ISoftDelete
    {
        [Key]
        public int Id { get; set; }

        public DateTime StudyDate { get; set; }

        [StringLength(50)]
        public string? StudyName { get; set; }

        /// <summary>Informe en texto enriquecido (HTML).</summary>
        public string? Information { get; set; }

        public int PatientId { get; set; }
        public Patient? Patient { get; set; }

        public StudyStatus Status { get; set; } = StudyStatus.InProgress;

        public bool Processed { get; set; }

        [StringLength(400)]
        public string? LinkFile1 { get; set; }

        [StringLength(400)]
        public string? LinkFile2 { get; set; }

        [StringLength(400)]
        public string? LinkFile3 { get; set; }

        // ---- Firma ----------------------------------------------------------
        public DateTime? SignedAt { get; set; }
        public Guid? SignedById { get; set; }
        public User? SignedBy { get; set; }

        [StringLength(200)]
        public string? SignedByName { get; set; }

        /// <summary>PDF del informe firmado que se genera al firmar (\\{StudyFiles:Server}\{StudyFiles:Share}\...).</summary>
        [StringLength(400)]
        public string? SignedPdfPath { get; set; }

        // ---- Auditoría ------------------------------------------------------
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(200)]
        public string? CreatedByName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [StringLength(200)]
        public string? UpdatedByName { get; set; }

        public bool IsLocked => Status == StudyStatus.Completed;

        // ---- Borrado lógico (ISoftDelete) ----------------------------------------
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [StringLength(200)]
        public string? DeletedByName { get; set; }
    }

    public enum StudyStatus
    {
        InProgress = 0,
        ToSign = 1,
        Completed = 2
    }
}
