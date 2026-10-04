using StudiesFinal.Models.Entities;

namespace StudiesFinal.Web.Models.ViewModels
{
    /// <summary>Elementos eliminados (borrado lógico) para la página "Deleted items".</summary>
    public class TrashVM
    {
        public List<Study> Studies { get; set; } = new();
        public List<Patient> Patients { get; set; } = new();
        public List<StudyTemplate> Templates { get; set; } = new();
        public List<User> Users { get; set; } = new();
    }
}
