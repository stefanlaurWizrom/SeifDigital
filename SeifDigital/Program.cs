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

app.UseStaticFiles();
app.UseForwardedHeaders();

app.UseRouting();

app.UseSession();

// IMPORTANT: nu mai folosim Windows Negotiate
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
