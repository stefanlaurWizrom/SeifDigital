using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpOverrides;


var builder = WebApplication.CreateBuilder(args);
// Citim adresa serverului din appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Activăm conexiunea la SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 1. Spunem aplicației să folosească logarea de Windows (laptop)
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<SeifDigital.Services.AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddHostedService<AuditCleanupService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<UserNoteService>();
builder.Services.AddScoped<UserFileService>();


// 2. Activăm "Memoria" aplicației (Sesiunea) pentru a ține minte dacă ai băgat codul de email
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // pe IIS cu HTTPS devine Secure
});


builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.AddSingleton<EncryptionService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // IMPORTANT: dacă nu setezi KnownProxies/KnownNetworks, iar ForwardLimit e default,
    // în dev de obicei merge. Pentru producție, e bine să limitezi.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Dacă ai un reverse proxy / load balancer cu IP fix, îl adaugi aici, ex:
    // options.KnownProxies.Add(System.Net.IPAddress.Parse("10.0.0.10"));
});


var app = builder.Build();

app.UseStaticFiles();
app.UseForwardedHeaders();
app.UseRouting();

// Activăm memoria de sesiune
app.UseSession();

// Activăm sistemul de logare
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
