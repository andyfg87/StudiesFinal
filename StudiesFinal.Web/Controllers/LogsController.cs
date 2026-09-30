using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Paginations;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class LogsController : Controller
    {
        private readonly IAppLogger _logger;

        public LogsController(IAppLogger logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 50, string? level = null, string? search = null)
        {
            var query = (await _logger.ApplicationLogs()).AsNoTracking();

            if (!string.IsNullOrEmpty(level))
                query = query.Where(l => l.Level == level);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var like = $"%{search.Trim()}%";
                query = query.Where(l => EF.Functions.Like(l.Message, like)
                                      || EF.Functions.Like(l.User, like)
                                      || EF.Functions.Like(l.Parameters!, like));
            }

            var routeValues = new RouteValueDictionary { ["level"] = level, ["search"] = search };
            var page = await PaginatedList<ApplicationLog>.CreateAsync(query.OrderByDescending(l => l.Id), pageNumber, pageSize, routeValues);

            return View(page);
        }
    }
}
