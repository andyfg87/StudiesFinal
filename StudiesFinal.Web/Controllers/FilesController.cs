using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Extensions;
using StudiesFinal.Web.Services;
using StudiesFinal.Web.Utils;

namespace StudiesFinal.Web.Controllers
{
    /// <summary>
    /// Explorador de archivos del servidor para elegir los archivos de un estudio sin subirlos:
    /// solo se guarda la ruta. Recorre las ubicaciones de StudyFiles:BrowseRoots (p. ej. todo
    /// \\192.168.199.140\Fileserver) con rutas del tipo "Ubicación\carpeta\archivo", así que
    /// nunca expone la ruta del servidor ni permite salir de esas ubicaciones.
    /// </summary>
    [Authorize(Roles = Roles.All)]
    public class FilesController : Controller
    {
        private const int MaxFiles = 300;

        private static readonly HashSet<string> HiddenNames = new(StringComparer.OrdinalIgnoreCase)
            { "Thumbs.db", "desktop.ini", ".DS_Store" };

        private readonly IStudyFileService _files;
        private readonly IAppLogger _logger;

        public FilesController(IStudyFileService files, IAppLogger logger)
        {
            _files = files;
            _logger = logger;
        }

        /// <summary>
        /// Contenido de una carpeta (JSON). dir = ruta del explorador ("Ubicación\carpeta";
        /// vacío = lista de ubicaciones). q filtra por nombre. Los archivos van del más
        /// reciente al más antiguo.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Browse(string? dir, string? q)
        {
            var filter = q?.Trim();
            bool Matches(string name) => string.IsNullOrEmpty(filter) || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

            // Primer nivel: las ubicaciones configuradas (StudyFiles:BrowseRoots)
            if (string.IsNullOrWhiteSpace(dir))
            {
                var locations = _files.Locations
                    .Where(l => Matches(l.Name))
                    .Select(l => new { name = l.Name, dir = l.Name })
                    .ToList();
                return Json(new
                {
                    dir = "",
                    crumbs = new[] { new { name = "Locations", dir = "" } },
                    folders = locations,
                    files = Array.Empty<object>(),
                    truncated = false,
                    total = 0
                });
            }

            var full = _files.Resolve(dir);
            if (full == null)
                return BadRequest(new { error = "Invalid folder." });

            var relDir = _files.ToBrowserPath(full) ?? "";

            try
            {
                var info = new DirectoryInfo(full);
                if (!info.Exists)
                    return NotFound(new { error = "Folder not found." });

                var folders = info.EnumerateDirectories()
                    .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) && !d.Attributes.HasFlag(FileAttributes.System))
                    .Where(d => Matches(d.Name))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new { name = d.Name, dir = Combine(relDir, d.Name) })
                    .ToList();

                var allFiles = info.EnumerateFiles()
                    .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden) && !f.Attributes.HasFlag(FileAttributes.System))
                    .Where(f => !HiddenNames.Contains(f.Name) && !f.Name.StartsWith("~$"))
                    .Where(f => Matches(f.Name))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                var files = allFiles.Take(MaxFiles).Select(f => new
                {
                    name = f.Name,
                    path = Combine(relDir, f.Name),
                    size = f.Length,
                    modified = f.LastWriteTime.ToString("MM/dd/yyyy h:mm tt")
                });

                // Migas: "Locations" + nombre de la ubicación + carpetas (nunca la ruta del servidor)
                var crumbs = new List<object> { new { name = "Locations", dir = "" } };
                var acc = "";
                foreach (var part in relDir.Split('\\', StringSplitOptions.RemoveEmptyEntries))
                {
                    acc = Combine(acc, part);
                    crumbs.Add(new { name = part, dir = acc });
                }

                return Json(new
                {
                    dir = relDir,
                    crumbs,
                    folders,
                    files,
                    truncated = allFiles.Count > MaxFiles,
                    total = allFiles.Count
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                await _logger.LogError("Cannot browse the studies folder", ex, nameof(FilesController), nameof(Browse), new { dir });
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "The studies server is not available. Try again later or contact the administrator." });
            }
        }

        /// <summary>Abre un archivo elegido en el explorador (antes de guardar el estudio).</summary>
        [HttpGet]
        public Task<IActionResult> Open(string? p, bool download = false)
        {
            var full = string.IsNullOrWhiteSpace(p) ? null : _files.Resolve(p);
            return this.ServeStudyFile(full, download, _logger);
        }

        private static string Combine(string dir, string name) => string.IsNullOrEmpty(dir) ? name : dir + "\\" + name;
    }
}
