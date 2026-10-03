using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudiesFinal.Models.EF;
using StudiesFinal.Models.Files;
using StudiesFinal.Models.Entities;
using StudiesFinal.Models.Interface;
using StudiesFinal.Web.Interface;
using StudiesFinal.Web.Repositories;
using StudiesFinal.Web.Services;
using StudiesFinal.Web.Utils;

var builder = WebApplication.CreateBuilder(args);

// SQL Server: LocalDB en desarrollo (appsettings.json) y SERVER01\SQLEXPRESS en el
// servidor (appsettings.Production.json).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing in appsettings.json.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

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
        "StudyFiles:Server is missing in appsettings.json (IP of the studies file server).")
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

// Interfaz en inglés y fechas MM/dd/yyyy, sin depender del idioma de Windows del servidor
var enUs = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = enUs;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = enUs;

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(enUs),
    SupportedCultures = new[] { enUs },
    SupportedUICultures = new[] { enUs }
});

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
