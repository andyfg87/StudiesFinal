using System.ComponentModel.DataAnnotations;

namespace StudiesFinal.Models.Entities
{
    public class ApplicationLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "INFO"; // "INFO", "WARNING", "ERROR"
        public string Message { get; set; } = string.Empty;
        public string? Logger { get; set; } // Nombre del controlador/service
        public string? Exception { get; set; }
        public string User { get; set; } = "System";
        public string? Action { get; set; }
        public string? Parameters { get; set; } // JSON de parámetros
    }
}
