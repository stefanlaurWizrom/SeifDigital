using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Filters;
using SeifDigital.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Forwarded headers (safe)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Global filter 2FA
builder.Services.AddScoped<Require2FAAttribute>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<Require2FAAttribute>();
});

// Services
builder.Services.AddScoped<AuditService>();
builder.Services.AddHostedService<AuditCleanupService>();

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<UserNoteService>();
builder.Services.AddScoped<UserFileService>();

builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<UserProfileService>();

builder.Services.AddScoped<UserAccountService>();

builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// IMPORTANT: nu mai folosim Windows Negotiate
// app.UseAuthentication();
app.UseAuthorization();

// ✅ NO-CACHE global pentru pagini HTML (rezolvă “Back” după Logout în IIS)
app.Use(async (context, next) =>
{
    // setăm headerele chiar înainte să se trimită răspunsul (mai sigur decât după next)
    context.Response.OnStarting(() =>
    {
        var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

        // Nu afecta fișierele statice (css/js/lib/etc.)
        bool isStatic =
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/lib/") ||
            path.StartsWith("/favicon") ||
            path == "/robots.txt" ||
            path.StartsWith("/images/") ||
            path.EndsWith(".css") ||
            path.EndsWith(".js") ||
            path.EndsWith(".png") ||
            path.EndsWith(".jpg") ||
            path.EndsWith(".jpeg") ||
            path.EndsWith(".gif") ||
            path.EndsWith(".svg") ||
            path.EndsWith(".webp") ||
            path.EndsWith(".ico");

        if (!isStatic)
        {
            // doar pentru răspunsuri de tip HTML (view-uri)
            var contentType = context.Response.ContentType ?? "";
            if (string.IsNullOrWhiteSpace(contentType) || contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
            }
        }

        return Task.CompletedTask;
    });

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
