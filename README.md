# StudiesFinal

Aplicación web (ASP.NET Core MVC, .NET 8, EF Core 9 + SQL Server) que sustituye a la base de datos
Access `StudiesFinal8.accdb` / `StudiesFinal1_be.accdb`. Sigue la misma estructura que
CapoteSolution: proyecto de modelos + proyecto web con repositorio genérico y
`AbstractEntityManagementController`.

## Proyectos

| Proyecto | Contenido |
|---|---|
| `StudiesFinal.Models` | Entidades (`User`, `Patient`, `Study`, `StudyTemplate`, `ApplicationLog`), interfaces y `ApplicationDbContext` (SQL Server) con sus migraciones en `EF/Migrations`. |
| `StudiesFinal.Web` | MVC: repositorios, controladores, vistas, login por cookies y reglas del flujo de estudios. |
| `StudiesFinal.Importer` | Consola (solo Windows, x64) que importa el back-end de Access a SQL Server. |

## Roles y flujo

Fases de un estudio: **En progreso → Por firmar → Completado**.

| | Técnico | Doctor | Admin |
|---|---|---|---|
| Ver todos los estudios | ✔ | ✔ | ✔ |
| Crear estudios (siempre nacen *En progreso*) | ✔ | ✔ | ✔ |
| Editar *En progreso* / enviar a firmar | ✔ | ✔ | ✔ |
| Editar *Por firmar* / devolver a técnicos | | ✔ | ✔ |
| Firmar | | ✔ | |
| Desbloquear un estudio firmado (vuelve a *Por firmar*, sin firma) | | ✔ | |
| Crear/modificar plantillas | | ✔ | ✔ |
| Usuarios y registro de actividad | | | ✔ |

Las reglas están en `StudiesFinal.Web/Services/StudyWorkflow.cs`.

### Borrado lógico (soft delete)

Estudios, pacientes, plantillas y usuarios **no se borran de la base de datos**: al eliminarlos se
marcan (`IsDeleted`, `DeletedAt`, `DeletedByName`) y desaparecen de todas las pantallas y búsquedas
(filtros globales en `ApplicationDbContext`; un `Remove` se guarda como marca). Los archivos del
servidor no se tocan.

- **Deleted items** (menú de administración, `TrashController`, solo Admin) lista lo eliminado,
  quién y cuándo, y permite restaurarlo. Al restaurar un estudio cuyo paciente también está
  eliminado, se restaura el paciente. Un usuario no se puede restaurar si otro activo ya usa su
  nombre de usuario.
- El número de un paciente eliminado sigue ocupado (no se puede crear otro con el mismo número).
- Para ver las filas eliminadas en SQL: `WHERE IsDeleted = 1`. El importador con `--replace`
  también borra las filas eliminadas.

## Archivos de los estudios

El servidor de archivos se configura en `appsettings.json`:

```json
"StudyFiles": {
  "Server": "192.168.199.140",
  "Share": "Fileserver\\Studies",
  "ReportsFolder": "Studies Report"
}
```

- Raíz de los estudios: `\\192.168.199.140\Fileserver\Studies` (la unidad `Z:\Studies` de los
  equipos). Al abrir un archivo, las rutas antiguas (`Z:\Studies`, `\\192.168.199.170\Studies`, …)
  se traducen a ella (`StudyFiles:LegacyPrefixes`).
- **Archivos del estudio (LinkFile1-3): no se suben.** Al crear o editar un estudio se eligen con
  el explorador de la propia web (*Browse…*, `FilesController`) y solo se guarda la ruta completa.
- **Ubicaciones del explorador (`StudyFiles:BrowseRoots`).** El explorador empieza mostrando una
  lista de ubicaciones con nombre y desde ahí se puede ir a cualquier carpeta que contengan:

  ```json
  "BrowseRoots": [
    { "Name": "Fileserver", "Path": "\\\\192.168.199.140\\Fileserver" },
    { "Name": "Scans",      "Path": "\\\\SERVER01\\Scans" }
  ]
  ```

  Para permitir otra carpeta basta con añadir otra entrada (la cuenta de la web necesita lectura).
  Si la raíz de los estudios no queda dentro de ninguna, se añade sola como "Studies". El navegador
  solo ve rutas del tipo `Fileserver\Studies\Holter\archivo.pdf`; el servidor las traduce y no
  permite salir de una ubicación (`..`, rutas absolutas o UNC escritas a mano se rechazan). El
  formulario nunca acepta rutas escritas a mano (`LinkFileN` es `[BindNever]`).
- **Por seguridad la web no muestra rutas** (ni en pantallas, ni en avisos, ni en errores): solo el
  nombre del archivo. Los archivos se abren a través de la web (`/Studies/OpenFile`, `/Files/Open`).
- Los PDF firmados van a `ReportsFolder`, una carpeta por tipo de reporte:

```
\\192.168.199.140\Fileserver\Studies\Studies Report\Holter Report\HR-Angulo Juan-09-30-2026.pdf
```

  Si ese día ya existe, se añade ` (2)`, ` (3)`… Cuando la carpeta del servidor no se llama igual
  que el tipo de estudio, se indica en `StudyFiles:Folders` (p. ej. "Exercise Stress Test Protocol
  Report" → "Excercise Stress Test Protocol Report"). Las iniciales del archivo se pueden cambiar
  en `StudyFiles:Prefixes`.
- La web lee y sirve los archivos desde el servidor, así que **la cuenta con la que corre la web
  necesita lectura en esa carpeta compartida (y escritura en `ReportsFolder` para los PDF firmados)**.

**PDF del informe al firmar:** cuando el doctor firma, se genera el PDF del informe
(mismo formato que la plantilla de Access: logo, cabecera del médico, firma electrónica y
"Final Report") con QuestPDF y se guarda con la misma regla de carpeta y nombre. La ruta queda
en `Study.SignedPdfPath`. Si el servidor no está disponible, la firma se mantiene y desde el
detalle del estudio se puede usar "Generar PDF" para reintentarlo. Los datos del médico están
en la sección `Report` de `appsettings.json`; la fuente Calibri se toma de `C:\Windows\Fonts`.

## Base de datos: SQL Server

Se usa SQL Server en todos los entornos para que no haya diferencias:

| Archivo | Entorno | Conexión (`ConnectionStrings:DefaultConnection`) |
|---|---|---|
| `appsettings.json` | Development (equipo local) | `(localdb)\MSSQLLocalDB`, base `StudiesFinal` |
| `appsettings.Production.json` | Production (servidor) | `SERVER01\SQLEXPRESS`, base `StudiesFinal` |

Las migraciones están en `StudiesFinal.Models/EF/Migrations`. Al arrancar, la web crea la base
si no existe y aplica las migraciones pendientes.

## Puesta en marcha (local)

Requiere SQL Server LocalDB (viene con Visual Studio).

```bash
dotnet run --project StudiesFinal.Web
```

- La base `StudiesFinal` se crea sola en LocalDB. Para llenarla con los datos de Access, usa el
  importador con `--environment Development` (ver abajo).
- Si no hay usuarios, se crea el administrador de `SeedAdmin` (en desarrollo está en
  `appsettings.Development.json`, que no se sube a git).
- Si LocalDB está detenida, la web y el importador la arrancan con `sqllocaldb start` antes de
  conectar (`LocalDbStarter`). Así no se queda un `sqlservr.exe` huérfano al detener la depuración
  en Visual Studio. Si aun así aparece *"SQL Server process failed to start"*:
  `Get-Process sqlservr | Stop-Process -Force; sqllocaldb start MSSQLLocalDB`.

## Instalación en el servidor

1. Copia el proyecto y revisa `StudiesFinal.Web/appsettings.Production.json`: servidor y base de
   datos de SQL Server. Con `Integrated Security=True`, la cuenta de Windows que ejecuta la web
   (o el importador) necesita permiso para crear la base, o crea la base vacía antes y dale permisos.
2. Importa los datos de Access (ver abajo). Crea la base y las tablas si no existen.
3. Para el primer administrador, define `SeedAdmin` antes del primer arranque, por ejemplo con
   variables de entorno (`SeedAdmin__Username`, `SeedAdmin__Password`) para no dejar la contraseña
   en un archivo. Solo se usa si la tabla de usuarios está vacía.
4. Arranca la web con `ASPNETCORE_ENVIRONMENT=Production` (es el valor por defecto).

## Importar desde Access

Requiere Microsoft Access Database Engine (ACE OLEDB) de 64 bits en el equipo donde se ejecuta.

```bash
dotnet run --project StudiesFinal.Importer -- --source "C:\ruta\StudiesFinal1_be.accdb"
```

- Usa la misma configuración que la web. Por defecto el entorno es **Production**, así que en el
  servidor importa a `SERVER01\SQLEXPRESS`. En tu equipo, para importar a LocalDB:
  `--environment Development`.
- También se puede indicar otra base:
  `--connection "Server=...;Database=StudiesFinal;Integrated Security=True;TrustServerCertificate=True;"`
- `--replace` borra estudios, pacientes y plantillas (no usuarios ni actividad) antes de importar.
  Sin `--replace`, si la base ya tiene datos no hace nada.
- Conserva los Id de Access (con `IDENTITY_INSERT`; los estudios nuevos siguen numerándose a partir
  del último importado). `Estado`/`isComplete` → fase; `Signature` → fecha de firma.
- Los estudios cuyo paciente no existe en Access reciben un paciente "(Patient N not found in Access)".
- Links (`LinkFile1-3`): se importan **tal cual están en Access** (`Z:\Studies\...`,
  `\\192.168.199.170\Studies\...`, etc.) y se corrigen después con un query en SQL Server. Solo se
  quitan espacios y el formato de hipervínculo de Access (`texto#ruta#`). Al abrir un archivo, la web
  sí traduce los prefijos antiguos de `StudyFiles:LegacyPrefixes` al servidor actual, y al editar un
  estudio los links que no se modifican se guardan sin cambios.

## Migraciones

```bash
dotnet ef migrations add Nombre --project StudiesFinal.Models --startup-project StudiesFinal.Models --output-dir EF/Migrations
```
