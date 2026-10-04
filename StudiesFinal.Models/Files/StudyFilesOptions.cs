namespace StudiesFinal.Models.Files
{
    /// <summary>
    /// Configuración "StudyFiles" de appsettings (la comparten la web y el importador).
    /// Los archivos de los estudios viven en la carpeta compartida \\{Server}\{Share};
    /// todos los links deben empezar por esa raíz.
    /// </summary>
    public class StudyFilesOptions
    {
        public const string Section = "StudyFiles";

        /// <summary>IP o nombre del servidor de archivos (p. ej. 192.168.199.140).</summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>Carpeta compartida dentro del servidor (p. ej. Fileserver\Studies).</summary>
        public string Share { get; set; } = "Studies";

        /// <summary>
        /// Opcional: ruta raíz completa. Si se indica, tiene prioridad sobre Server/Share
        /// (útil para pruebas con una carpeta local).
        /// </summary>
        public string? BasePath { get; set; }

        /// <summary>Raíz efectiva: BasePath o \\{Server}\{Share}.</summary>
        public string ResolveBasePath()
        {
            if (!string.IsNullOrWhiteSpace(BasePath))
                return BasePath.Trim().TrimEnd('\\', '/');

            if (string.IsNullOrWhiteSpace(Server))
                throw new InvalidOperationException(
                    "StudyFiles:Server is missing in appsettings.json (IP of the studies file server).");

            var server = Server.Trim().TrimStart('\\').TrimEnd('\\');
            var share = (Share ?? "").Trim().Trim('\\', '/');
            return string.IsNullOrEmpty(share) ? $@"\\{server}" : $@"\\{server}\{share}";
        }

        /// <summary>
        /// Prefijos antiguos (unidades mapeadas / servidores anteriores) que se reescriben
        /// a la raíz actual. P. ej. "Z:\Studies" o "\\192.168.199.170\Studies".
        /// </summary>
        public List<string> LegacyPrefixes { get; set; } = new();

        /// <summary>
        /// Excepciones de carpeta por tipo de reporte. Por defecto la carpeta es el propio
        /// nombre del reporte: \\{Server}\{Share}\Studies Report\Holter Report
        /// </summary>
        public Dictionary<string, string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Excepciones del prefijo del nombre de archivo. Por defecto son las iniciales
        /// del reporte: "Holter Report" -> HR.
        /// </summary>
        public Dictionary<string, string> Prefixes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Carpeta dentro de la raíz donde van todos los archivos de los estudios, una
        /// subcarpeta por tipo de reporte: \\{Server}\{Share}\Studies Report\Holter Report
        /// </summary>
        public string ReportsFolder { get; set; } = "Studies Report";

        /// <summary>Carpeta para estudios sin tipo de reporte.</summary>
        public string DefaultFolder { get; set; } = "Other";

        /// <summary>
        /// Ubicaciones que se pueden recorrer con el explorador de archivos de la web y desde
        /// las que se pueden abrir archivos (p. ej. todo \\192.168.199.140\Fileserver).
        /// La raíz de estudios (Server/Share) se permite siempre aunque no esté en la lista.
        /// </summary>
        public List<BrowseRoot> BrowseRoots { get; set; } = new();
    }

    /// <summary>Ubicación del explorador: nombre visible + ruta (no se muestra al usuario).</summary>
    public class BrowseRoot
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
