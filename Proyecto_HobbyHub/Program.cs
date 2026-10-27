using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Proyecto_HobbyHub.Configuration;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

var builder = WebApplication.CreateBuilder(args);
// Integral development is authorized only against the dedicated local database.
// Prevent an accidental CLI/IDE launch in Production from selecting shared configuration.
if (!DatabaseConnectionPolicy.RequiresIsolation(builder.Environment.EnvironmentName))
    throw new InvalidOperationException("Este checkout está habilitado solo en Development/Testing con SQL aislado. Configura ASPNETCORE_ENVIRONMENT=Development.");

// Select local configuration explicitly; development never falls back to the shared database.
if (DatabaseConnectionPolicy.RequiresIsolation(builder.Environment.EnvironmentName))
{
    // Development never loads the local configuration used by the shared database.
    builder.Configuration
        .AddJsonFile("appsettings.Development.Isolated.json", optional: false, reloadOnChange: false)
        .AddJsonFile("appsettings.Development.Local.json", optional: true, reloadOnChange: false);
    if (builder.Environment.IsDevelopment())
        builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: false);
}
else
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
}

// Explicit deployment/development overrides take precedence over local files.
builder.Configuration.AddEnvironmentVariables();
if (args.Length > 0)
    builder.Configuration.AddCommandLine(args);

// Configuración de la base de datos
// Reviewed categories exclude raw framework exceptions, SQL and request URLs.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = false; o.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ "; o.UseUtcTimestamp = true; });
builder.Logging.AddDebug();
builder.Services.PostConfigure<LoggerFilterOptions>(options =>
{
    // Apply after configuration so more-specific framework rules cannot expose SQL,
    // request URLs or raw exception messages through any provider.
    options.Rules.Clear();
    options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information,
        (_, category, level) => category != null &&
            (category.StartsWith("Proyecto_HobbyHub.", StringComparison.Ordinal) || category == "SecurityRequest") && level >= LogLevel.Information));
});
builder.Logging.AddProvider(new RollingFileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, ".local", "logs"),
    builder.Configuration.GetValue<long?>("TechnicalLogs:MaxFileBytes") ?? 5242880,
    builder.Configuration.GetValue<int?>("TechnicalLogs:RetainedFiles") ?? 14,
    builder.Configuration.GetValue<int?>("TechnicalLogs:RetentionDays") ?? 14));
builder.Services.AddSingleton<AuditEventBudget>();

string connectionString = DatabaseConnectionPolicy.Resolve(
    builder.Configuration, builder.Environment.EnvironmentName);
if (builder.Environment.IsDevelopment())
{
    var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    bool visualTestDatabase=builder.Configuration.GetValue<bool>("VisualTesting:Enabled")&&
        System.Text.RegularExpressions.Regex.IsMatch(target.InitialCatalog,"^HobbyHub_Development_Visual_20261008_[a-f0-9]{8}$");
    if (!target.DataSource.Equals("(localdb)\\HobbyHubPhase1C_20261008", StringComparison.OrdinalIgnoreCase) ||
        (!target.InitialCatalog.Equals("HobbyHub_Development_Phase1C_20261008", StringComparison.OrdinalIgnoreCase)&&!visualTestDatabase))
        throw new InvalidOperationException("Development admite únicamente la instancia y base aisladas autorizadas para este checkout.");
}

builder.Services.AddDbContext<HobbyHubContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new ManagementAuthorizationConvention());
    options.Conventions.Add(new SocialWriteConvention());
    options.Filters.AddService<ManagementAuditFilter>();
    options.Filters.AddService<WriteRequestAuditFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(32 * 1024 * 1024));
    options.Filters.Add(new SafePersistenceExceptionFilter());
});
builder.Services.AddScoped<ManagementAuditFilter>();
builder.Services.AddScoped<WriteRequestAuditFilter>();
builder.Services.AddScoped<ISecurityAudit, EfSecurityAudit>();
builder.Services.AddScoped<IAccountRecoveryStore, EfAccountRecoveryStore>();
builder.Services.AddScoped<IRecoveryMailSender, LocalRecoveryMailbox>();
builder.Services.AddScoped<AccountRecoveryService>();
builder.Services.AddScoped<SocialAccess>();
builder.Services.AddScoped<ImageUploadService>();
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAccountSecurityStore, EfAccountSecurityStore>();
builder.Services.AddScoped<AccountSecurityService>();
builder.Services.AddScoped<RevalidatingCookieEvents>();

var protection = builder.Services.AddDataProtection().SetApplicationName("HobbyHub");
if (builder.Environment.IsEnvironment("Testing"))
    protection.UseEphemeralDataProtectionProvider();
else
{
    string keyPath = Path.GetFullPath(builder.Configuration["Security:DataProtection:KeyPath"] ??
        Path.Combine(builder.Environment.ContentRootPath, ".local", "keys"));
    string webRoot = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
    if (keyPath.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
        keyPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Las claves de protección deben almacenarse fuera de wwwroot.");
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
    if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
    else if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Configura protección de claves para el sistema de despliegue antes de iniciar este entorno.");
}

// Configuración de Autenticación por Cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccesoDenegado";
        options.Cookie.Name = builder.Configuration.GetValue<bool>("VisualTesting:Enabled") ? "HobbyHub.Visual.Auth" : "HobbyHub.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = AccountSecurityService.SessionLifetime;
        options.EventsType = typeof(RevalidatingCookieEvents);
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(SecurityPolicies.Administrator, policy => policy.RequireRole("Administrador"));
    options.AddPolicy(SecurityPolicies.Moderation, policy => policy.RequireRole("Moderador", "Administrador"));
    options.AddPolicy(SecurityPolicies.CreateCommunity, policy => policy.RequireRole("Creador"));
});
builder.Services.AddSingleton<RequestSecurityLog>();
builder.Services.AddRateLimiter(options => RequestLimits.Configure(options, builder.Configuration));

var app = builder.Build();
if(await LocalDevelopmentTools.Run(app))return;
app.UseMiddleware<TechnicalRequestMiddleware>();
app.UseStatusCodePagesWithReExecute("/Home/Estado/{0}");
app.Use(async (context,next)=>
{
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["X-Frame-Options"]="DENY";
    context.Response.Headers["Referrer-Policy"]="same-origin";
    context.Response.OnStarting(()=>{
        if(context.User.Identity?.IsAuthenticated==true||context.Request.Path.StartsWithSegments("/Auth")||context.Request.Path.StartsWithSegments("/Cuenta"))
            context.Response.Headers.CacheControl="no-store";
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    using var scope = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SecurityRequest")
        .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
    await next();
    if (context.Response.StatusCode is 401 or 403 ||
        context.Response.StatusCode == 302 && context.Response.Headers.Location.ToString().StartsWith("/Auth/AccesoDenegado", StringComparison.Ordinal))
        context.RequestServices.GetRequiredService<RequestSecurityLog>().Rejected(context, "AuthorizationDenied");
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();

// Entry point for the offline MVC regression tests.
public partial class Program { }
