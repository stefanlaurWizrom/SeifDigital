using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Filters;          // <-- IMPORTANT (filtrul global 2FA)
using SeifDigital.Services;

var builder = WebApplication.CreateBuilder(args);

// Citim adresa serverului din appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Activăm conexiunea la SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 1) Autentificare Windows (IIS / Negotiate)
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

// 2) Sesiune (ține minte Status2FA)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // pe IIS cu HTTPS devine Secure
});

// 3) Forwarded Headers (IP real prin proxy/LB)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    // options.KnownProxies.Add(System.Net.IPAddress.Parse("10.0.0.10"));
});

// 4) Înregistrăm filtrul global 2FA
builder.Services.AddScoped<Require2FAAttribute>();

// 5) MVC + aplicăm filtrul global (se execută pe TOATE controllerele/acțiunile)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<Require2FAAttribute>();
});

// 6) Servicii aplicație
builder.Services.AddScoped<AuditService>();
builder.Services.AddHostedService<AuditCleanupService>();

builder.Services.AddScoped<SettingsService>();   // <-- păstrăm o singură dată (înainte era duplicat)
builder.Services.AddScoped<UserNoteService>();
builder.Services.AddScoped<UserFileService>();

builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<UserProfileService>();


builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.AddSingleton<EncryptionService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseForwardedHeaders();

app.UseRouting();

// IMPORTANT: Session trebuie să fie înainte de Authorization (ca filtrul să poată citi Status2FA)
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
