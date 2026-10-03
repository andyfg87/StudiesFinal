using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace StudiesFinal.Models.EF
{
    /// <summary>
    /// Arranca LocalDB con "sqllocaldb start" antes de conectar (solo si la cadena es (localdb)\...).
    ///
    /// Si es la propia app la que arranca LocalDB al conectar y luego el depurador de Visual
    /// Studio la cierra de golpe, el sqlservr.exe se queda huérfano: sigue funcionando pero
    /// LocalDB lo da por detenido y las conexiones siguientes fallan con
    /// "Error occurred during LocalDB instance startup: SQL Server process failed to start".
    /// Arrancándola con sqllocaldb.exe (que termina de forma normal) la instancia queda bien
    /// registrada. En el servidor (SQL Server normal) no hace nada.
    /// </summary>
    public static class LocalDbStarter
    {
        /// <returns>Un mensaje si hubo que arrancar la instancia; null si no hacía falta.</returns>
        /// <exception cref="InvalidOperationException">Si no se pudo arrancar; el mensaje explica cómo arreglarlo.</exception>
        public static string? EnsureStarted(string connectionString)
        {
            string dataSource;
            try
            {
                dataSource = new SqlConnectionStringBuilder(connectionString).DataSource ?? "";
            }
            catch (ArgumentException)
            {
                return null;
            }

            const string prefix = "(localdb)\\";
            if (!dataSource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            var instance = dataSource[prefix.Length..].Trim();
            if (instance.Length == 0 || instance.StartsWith('.'))
                return null; // instancias compartidas (localdb)\.\nombre: no se gestionan aquí

            var info = Run("info", instance);
            if (info.ExitCode == NotInstalled || info.Output.Contains("Running", StringComparison.OrdinalIgnoreCase))
                return null;

            var start = Run("start", instance);
            if (start.ExitCode == 0)
                return $"LocalDB instance '{instance}' was stopped and has been started.";

            throw new InvalidOperationException(
                $"LocalDB instance '{instance}' could not be started: {start.Output.Trim()}{Environment.NewLine}" +
                "Usually a previous SQL Server process was left running in the background. Fix it in PowerShell with:" +
                Environment.NewLine + "    Get-Process sqlservr | Stop-Process -Force; sqllocaldb start " + instance);
        }

        private static (int ExitCode, string Output) Run(string verb, string instance)
        {
            try
            {
                var psi = new ProcessStartInfo("sqllocaldb", $"{verb} \"{instance}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(60_000);
                return (p.HasExited ? p.ExitCode : -1, output);
            }
            catch (Exception ex)
            {
                // sqllocaldb no está en el PATH: se deja que la conexión normal lo intente
                return (NotInstalled, ex.Message);
            }
        }

        private const int NotInstalled = -2;
    }
}
