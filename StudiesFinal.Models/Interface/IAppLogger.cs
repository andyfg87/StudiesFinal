using StudiesFinal.Models.Entities;

namespace StudiesFinal.Models.Interface
{
    public interface IAppLogger
    {
        Task LogInformation(string message, string? logger = null, string? action = null, object? parameters = null);
        Task LogWarning(string message, string? logger = null, string? action = null, object? parameters = null);
        Task LogError(string message, Exception? ex = null, string? logger = null, string? action = null, object? parameters = null);
        Task<IQueryable<ApplicationLog>> ApplicationLogs();
    }
}
