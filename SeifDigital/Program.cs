using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Filters;
using SeifDigital.Services;
using Hangfire;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ✅ Hangfire Configuration
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        UseRecommendedIsolationLevel = true,
        UsePageLocksOnDequeue = true,
        DisableGlobalLocks = true
    });
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
    options.ServerTimeout = TimeSpan.FromMinutes(4);
});

// ✅ NOU: Mărire limită upload fișiere (default ~28.6 MB)
// Setare pentru Kestrel server
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 100_000_000; // 100 MB
});

// Setare pentru IIS (dacă se folosește)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100_000_000; // 100 MB
});

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

// ✅ Certificate Management Services
builder.Services.AddScoped<CertificateCheckService>();
builder.Services.AddScoped<CertificateSettingsService>();
builder.Services.AddScoped<CertificateScheduledCheckService>();

builder.Services.Configure<CryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// ✅ Hangfire Dashboard (admin only access)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

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

// ✅ Configure Hangfire Recurring Job for Certificate Verification
// Executes daily at configured hour and minute (default: 8:00 AM)
using (var scope = app.Services.CreateScope())
{
    var settingsService = scope.ServiceProvider.GetRequiredService<CertificateSettingsService>();
    var verificationHour = await settingsService.GetVerificationHourAsync();
    var verificationMinute = await settingsService.GetVerificationMinuteAsync();
    var timezone = await settingsService.GetTimezoneAsync();

    // Cron expression: "{minute} {hour} * * *" means at HH:MM every day
    // Example: "0 8 * * *" = 08:00 every day, "30 14 * * *" = 14:30 every day
    string cronExpression = $"{verificationMinute} {verificationHour} * * *";

    RecurringJob.AddOrUpdate<CertificateScheduledCheckService>(
        "certificate-check",
        service => service.ExecuteAsync(),
        cronExpression,
        TimeZoneInfo.FindSystemTimeZoneById(timezone));

    System.Diagnostics.Debug.WriteLine($"[Startup] Hangfire job registered - Cron: {cronExpression}, Timezone: {timezone}");
}

app.Run();

