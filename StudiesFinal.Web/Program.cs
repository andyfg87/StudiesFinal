using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Repositories;
using StudiesFinal.Web.Services;
using StudiesFinal.Web.Utils;

var builder = WebApplication.CreateBuilder(args);

// SQLite: una ruta relativa se resuelve contra la carpeta del proyecto/publicación,
// no contra el directorio desde el que se arranca el proceso.
var sqlite = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
if (!Path.IsPathRooted(sqlite.DataSource))
    sqlite.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqlite.DataSource);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(sqlite.ToString()));

builder.Services.AddControllersWithViews();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();

// Configuración de repositorios
builder.Services.AddScoped<IEntityRepository<User, Guid>, EntityRepository<User, Guid>>();
builder.Services.AddScoped<IEntityRepository<Patient, int>, EntityRepository<Patient, int>>();
builder.Services.AddScoped<IEntityRepository<Study, int>, EntityRepository<Study, int>>();
builder.Services.AddScoped<IEntityRepository<StudyTemplate, int>, EntityRepository<StudyTemplate, int>>();
builder.Services.AddScoped<IAppLogger, DatabaseLogger>();

// Servicios de la aplicación
// Servidor de archivos de los estudios: StudyFiles:Server (IP) y StudyFiles:Share en appsettings.json.
// Se valida al arrancar para que una IP que falte se vea enseguida.
builder.Services.AddOptions<StudyFilesOptions>()
    .Bind(builder.Configuration.GetSection(StudyFilesOptions.Section))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BasePath) || !string.IsNullOrWhiteSpace(o.Server),
        "Falta StudyFiles:Server en appsettings.json (IP del servidor de archivos de los estudios).")
    .ValidateOnStart();
builder.Services.AddScoped<IStudyFileService, StudyFileService>();
builder.Services.AddSingleton<IRichTextSanitizer, RichTextSanitizer>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();

// QuestPDF: licencia Community (gratuita para empresas con ingresos < 1 M USD/año)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
ReportPdfService.RegisterFonts();

// Autenticación por cookies (la tabla Users es el login: Admin, Doctor y Técnico)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // Todo requiere sesión salvo lo marcado con [AllowAnonymous]
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole(Roles.Doctor));
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
