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

## Archivos de los estudios

El servidor de archivos se configura en `appsettings.json`:

```json
"StudyFiles": {
  "Server": "192.168.199.140",
  "Share": "Studies"
}
```

Los links se guardan como ruta completa y siempre bajo `\\{Server}\{Share}`
(`\\192.168.199.140\Studies`). Para pruebas se puede poner `StudyFiles:BasePath` con una
carpeta local, que tiene prioridad sobre `Server`/`Share`. Las rutas antiguas (`Z:\Studies`, `\\192.168.199.170\Studies`,
`\\192.168.199.227\FileServer\Studies`…) se reescriben al importar y al guardar
(`StudyFiles:LegacyPrefixes`). La web sirve los archivos desde el servidor en
`/Studies/OpenFile`, así que **la cuenta con la que corre la web necesita acceso de lectura
(y escritura para las subidas) a esa carpeta compartida**.

Los archivos subidos se guardan en la carpeta del tipo de reporte, con las iniciales del
reporte, el paciente y la fecha de hoy:

```
\\192.168.199.140\Studies\Holter Report\HR-Angulo Juan-09-30-2026.pdf
```

Si ese día ya existe, se añade ` (2)`, ` (3)`…

**PDF del informe al firmar:** cuando el doctor firma, se genera el PDF del informe
(mismo formato que la plantilla de Access: logo, cabecera del médico, firma electrónica y
"Final Report") con QuestPDF y se guarda con la misma regla de carpeta y nombre. La ruta queda
en `Study.SignedPdfPath`. Si el servidor no está disponible, la firma se mantiene y desde el
detalle del estudio se puede usar "Generar PDF" para reintentarlo. Los datos del médico están
en la sección `Report` de `appsettings.json`; la fuente Calibri se toma de `C:\Windows\Fonts`.

Para cambiar la carpeta o las iniciales de un tipo concreto: `StudyFiles:Folders` y
`StudyFiles:Prefixes` (clave = nombre del reporte).

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
- Los estudios cuyo paciente no existe en Access reciben un paciente "(Paciente N no encontrado en Access)".

## Migraciones

```bash
dotnet ef migrations add Nombre --project StudiesFinal.Models --startup-project StudiesFinal.Models --output-dir EF/Migrations
```
