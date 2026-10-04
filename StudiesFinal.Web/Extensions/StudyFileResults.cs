using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using StudiesFinal.Models.Interface;

namespace StudiesFinal.Web.Extensions
{
    /// <summary>
    /// Sirve un archivo del servidor de estudios (PDF, JPG…) para verlo en el navegador o
    /// descargarlo. Si no existe o no se puede leer, muestra Views/Shared/FileError sin la ruta.
    /// </summary>
    public static class StudyFileResults
    {
        public static async Task<IActionResult> ServeStudyFile(this Controller controller, string? fullPath, bool download, IAppLogger logger)
        {
            var fileName = string.IsNullOrEmpty(fullPath) ? null : Path.GetFileName(fullPath);

            if (string.IsNullOrEmpty(fullPath))
                return FileError(controller, "File not available.", fileName);

            try
            {
                if (!System.IO.File.Exists(fullPath))
                    return FileError(controller, "File not found.", fileName);
            }
            catch (Exception ex)
            {
                await logger.LogError("Cannot access the studies folder", ex, controller.GetType().Name, nameof(ServeStudyFile), new { fullPath });
                return FileError(controller, "The studies server is not available.", fileName);
            }

            if (!new FileExtensionContentTypeProvider().TryGetContentType(fullPath, out var contentType))
                contentType = "application/octet-stream";

            if (download)
                return controller.PhysicalFile(fullPath, contentType, fileName); // attachment

            // inline: se abre en el visor del navegador
            controller.Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
            return controller.PhysicalFile(fullPath, contentType);
        }

        public static ViewResult FileError(Controller controller, string message, string? fileName)
        {
            controller.Response.StatusCode = StatusCodes.Status404NotFound;
            controller.ViewBag.Message = message;
            controller.ViewBag.FileName = fileName;
            return controller.View("FileError");
        }
    }
}
