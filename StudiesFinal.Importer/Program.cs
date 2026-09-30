using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Entities;
using System.Data.OleDb;
using System.Globalization;

// =========================================================================
// Importa el back-end de Access (StudiesFinal1_be.accdb) a la base SQLite.
//
//   dotnet run --project StudiesFinal.Importer -- --source "ruta\StudiesFinal1_be.accdb"
//        [--db "ruta\studiesfinal.db"] [--settings "ruta\appsettings.json"] [--replace]
//
// - Conserva los Id de Access (PatientID, ID de estudio y de plantilla).
// - Estado/isComplete -> Status; Signature (fecha) -> SignedAt.
// - Reescribe las rutas antiguas (Z:\Studies, \\192.168.199.170\Studies…) al
//   servidor configurado en StudyFiles:Server / StudyFiles:Share (\\<IP>\Studies).
// - No toca usuarios ni registros de actividad.
// =========================================================================

var options = ParseArgs(args);
if (!options.TryGetValue("source", out var source) || !File.Exists(source))
{
    Console.Error.WriteLine("Uso: --source <StudiesFinal1_be.accdb> [--db <studiesfinal.db>] [--settings <appsettings.json>] [--replace]");
    return 1;
}

var root = FindSolutionRoot();
var settingsPath = options.GetValueOrDefault("settings") ?? Path.Combine(root, "StudiesFinal.Web", "appsettings.json");
var dbPath = options.GetValueOrDefault("db") ?? Path.Combine(root, "StudiesFinal.Web", "App_Data", "studiesfinal.db");
var replace = options.ContainsKey("replace");

var config = new ConfigurationBuilder().AddJsonFile(settingsPath, optional: true).Build();
// Misma configuración que la web: StudyFiles:Server (IP) + StudyFiles:Share, o StudyFiles:BasePath
var basePath = config["StudyFiles:BasePath"];
if (string.IsNullOrWhiteSpace(basePath))
{
    var server = config["StudyFiles:Server"]?.Trim().Trim('\\');
    if (string.IsNullOrWhiteSpace(server))
    {
        Console.Error.WriteLine($"Falta StudyFiles:Server en {settingsPath}.");
        return 1;
    }
    var share = (config["StudyFiles:Share"] ?? "Studies").Trim().Trim('\\', '/');
    basePath = $@"\\{server}\{share}";
}
basePath = basePath.TrimEnd('\\');
var legacyPrefixes = config.GetSection("StudyFiles:LegacyPrefixes").Get<string[]>()
                     ?? new[] { @"Z:\Studies", @"Y:\Studies", @"\\192.168.199.170\Studies" };

Console.WriteLine($"Origen : {source}");
Console.WriteLine($"Destino: {dbPath}");
Console.WriteLine($"Links  : {string.Join(", ", legacyPrefixes)} -> {basePath}");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={dbPath}").Options;

await using var db = new ApplicationDbContext(dbOptions);
await db.Database.MigrateAsync();

if (await db.Studies.AnyAsync() || await db.Patients.AnyAsync() || await db.StudyTemplates.AnyAsync())
{
    if (!replace)
    {
        Console.Error.WriteLine("La base de datos ya tiene datos. Usa --replace para borrarlos (usuarios y actividad se conservan).");
        return 2;
    }

    Console.WriteLine("Borrando estudios, pacientes y plantillas existentes…");
    await db.Studies.ExecuteDeleteAsync();
    await db.Patients.ExecuteDeleteAsync();
    await db.StudyTemplates.ExecuteDeleteAsync();
}

using var access = OpenAccess(source);
db.ChangeTracker.AutoDetectChangesEnabled = false;
var us = CultureInfo.GetCultureInfo("en-US");

// ---- Plantillas ------------------------------------------------------------
var templates = 0;
foreach (var r in Read(access, "SELECT ID, StudyTitle, StudyInfo, GenericName FROM StudyTemplate"))
{
    db.StudyTemplates.Add(new StudyTemplate
    {
        Id = Convert.ToInt32(r["ID"]),
        StudyTitle = Str(r["StudyTitle"], 50) ?? $"Plantilla {r["ID"]}",
        StudyInfo = Str(r["StudyInfo"]),
        GenericName = Str(r["GenericName"], 50)
    });
    templates++;
}
await db.SaveChangesAsync();
Console.WriteLine($"Plantillas: {templates}");

// ---- Pacientes ---------------------------------------------------------------
var patientIds = new HashSet<int>();
foreach (var r in Read(access, "SELECT PatientID, PatientName, DOB FROM Patienttbl"))
{
    var id = Convert.ToInt32(r["PatientID"]);
    if (!patientIds.Add(id)) continue;

    db.Patients.Add(new Patient
    {
        Id = id,
        Name = Str(r["PatientName"], 100),
        DateOfBirth = r["DOB"] is DateTime dob ? dob.Date : null
    });

    if (patientIds.Count % 1000 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
}
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Console.WriteLine($"Pacientes: {patientIds.Count}");

// ---- Estudios ------------------------------------------------------------------
int studies = 0, orphans = 0, noDate = 0, rewritten = 0;
var statusCount = new Dictionary<StudyStatus, int>();

foreach (var r in Read(access, "SELECT ID, StudyDate, Information, PatientFK, Estado, LinkFile1, LinkFile2, LinkFile3, Processed, StudyName, Signature, isComplete FROM StudyTBL"))
{
    var patientId = r["PatientFK"] is DBNull ? 0 : Convert.ToInt32(r["PatientFK"]);

    // Estudios cuyo paciente ya no existe: se crea un paciente "desconocido" con ese Id
    if (patientIds.Add(patientId))
    {
        db.Patients.Add(new Patient { Id = patientId, Name = $"(Paciente {patientId} no encontrado en Access)" });
        orphans++;
    }

    var signed = string.Equals(Str(r["Estado"]), "Signed", StringComparison.OrdinalIgnoreCase);
    var complete = string.Equals(Str(r["isComplete"]), "Yes", StringComparison.OrdinalIgnoreCase);
    var status = signed ? StudyStatus.Completed : complete ? StudyStatus.ToSign : StudyStatus.InProgress;
    statusCount[status] = statusCount.GetValueOrDefault(status) + 1;

    DateTime? signedAt = DateTime.TryParse(Str(r["Signature"]), us, DateTimeStyles.None, out var sa) ? sa : null;

    var studyDate = r["StudyDate"] is DateTime d ? d.Date : (DateTime?)null;
    if (studyDate == null) noDate++;

    string? Link(object value)
    {
        var raw = Str(value);
        var normalized = NormalizeLink(raw, basePath, legacyPrefixes);
        if (raw != null && normalized != null && !normalized.Equals(raw, StringComparison.OrdinalIgnoreCase)) rewritten++;
        return normalized is { Length: > 400 } ? normalized[..400] : normalized;
    }

    db.Studies.Add(new Study
    {
        Id = Convert.ToInt32(r["ID"]),
        StudyDate = studyDate ?? new DateTime(1900, 1, 1),
        Information = Str(r["Information"]),
        PatientId = patientId,
        StudyName = Str(r["StudyName"], 50),
        Status = status,
        Processed = string.Equals(Str(r["Processed"]), "Yes", StringComparison.OrdinalIgnoreCase),
        LinkFile1 = Link(r["LinkFile1"]),
        LinkFile2 = Link(r["LinkFile2"]),
        LinkFile3 = Link(r["LinkFile3"]),
        SignedAt = status == StudyStatus.Completed ? signedAt : null,
        SignedByName = status == StudyStatus.Completed ? "Firma importada de Access" : null,
        CreatedAt = studyDate ?? DateTime.Now,
        CreatedByName = "Importado de Access"
    });

    if (++studies % 500 == 0) { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
}
await db.SaveChangesAsync();

Console.WriteLine($"Estudios: {studies}  (En progreso {statusCount.GetValueOrDefault(StudyStatus.InProgress)}, " +
                  $"Por firmar {statusCount.GetValueOrDefault(StudyStatus.ToSign)}, Completados {statusCount.GetValueOrDefault(StudyStatus.Completed)})");
Console.WriteLine($"Pacientes creados para estudios huérfanos: {orphans}");
Console.WriteLine($"Estudios sin fecha (puestos a 01/01/1900): {noDate}");
Console.WriteLine($"Links reescritos a {basePath}: {rewritten}");
Console.WriteLine("Importación terminada.");
return 0;

// =========================================================================

static OleDbConnection OpenAccess(string path)
{
    foreach (var provider in new[] { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" })
    {
        try
        {
            var c = new OleDbConnection($"Provider={provider};Data Source={path};Mode=Read;Persist Security Info=False");
            c.Open();
            return c;
        }
        catch (InvalidOperationException) { /* proveedor no registrado: probar el siguiente */ }
    }
    throw new InvalidOperationException("No está instalado Microsoft Access Database Engine (ACE OLEDB) de 64 bits.");
}

static IEnumerable<Dictionary<string, object>> Read(OleDbConnection c, string sql)
{
    using var cmd = new OleDbCommand(sql, c);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.GetValue(i);
        yield return row;
    }
}

static string? Str(object value, int? max = null)
{
    if (value is DBNull or null) return null;
    var s = value.ToString()!.Trim();
    if (s.Length == 0) return null;
    return max.HasValue && s.Length > max.Value ? s[..max.Value] : s;
}

// Mismo criterio que StudyFileService.Normalize en la web
static string? NormalizeLink(string? path, string basePath, string[] legacyPrefixes)
{
    if (string.IsNullOrWhiteSpace(path)) return null;

    var p = path.Trim().Trim('"').Replace('/', '\\');

    // Hipervínculo de Access: "texto#dirección#"
    if (p.Contains('#'))
    {
        var parts = p.Split('#');
        p = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : parts[0];
    }

    foreach (var prefix in legacyPrefixes)
    {
        var legacy = prefix.TrimEnd('\\');
        if (p.StartsWith(legacy + "\\", StringComparison.OrdinalIgnoreCase))
            return basePath + p[legacy.Length..];
    }

    return p;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var key = args[i][2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
        result[key] = value;
    }
    return result;
}

static string FindSolutionRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "StudiesFinal.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
