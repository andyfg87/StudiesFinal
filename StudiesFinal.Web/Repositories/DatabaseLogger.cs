using StudiesFinal.Models.EF;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;

namespace StudiesFinal.Web.Repositories
{
    public class DatabaseLogger : IAppLogger
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DatabaseLogger(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task LogInformation(string message, string? logger = null, string? action = null, object? parameters = null)
            => LogAsync("INFO", message, null, logger, action, parameters);

        public Task LogWarning(string message, string? logger = null, string? action = null, object? parameters = null)
            => LogAsync("WARNING", message, null, logger, action, parameters);

        public Task LogError(string message, Exception? ex = null, string? logger = null, string? action = null, object? parameters = null)
            => LogAsync("ERROR", message, ex?.ToString(), logger, action, parameters);

        private async Task LogAsync(string level, string message, string? exception, string? logger, string? action, object? parameters)
        {
            var log = new ApplicationLog
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Logger = logger,
                Exception = exception,
                Action = action,
                Parameters = parameters is string s ? s : parameters?.ToSanitizedJson(),
                User = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System"
            };

            _context.ApplicationLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public Task<IQueryable<ApplicationLog>> ApplicationLogs()
            => Task.FromResult<IQueryable<ApplicationLog>>(_context.ApplicationLogs);
    }
}
