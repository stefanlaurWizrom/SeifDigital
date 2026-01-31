using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SeifDigital.Data;

namespace SeifDigital.Services
{
    // rulează periodic (1x/zi) și aplică retenția setată de admin
    public class AuditCleanupService : BackgroundService
    {
        private readonly IServiceProvider _sp;

        public AuditCleanupService(IServiceProvider sp)
        {
            _sp = sp;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // rulează imediat la start, apoi la 24h
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnce(stoppingToken);

                // așteaptă 24h
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task RunOnce(CancellationToken stoppingToken)
        {
            int days = 90;
            int deleted = 0;

            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();

                days = await settings.GetIntAsync("AuditRetentionDays", 90);

                // safety: nu permitem valori absurde
                if (days < 7) days = 7;
                if (days > 3650) days = 3650;

                var cutoff = DateTime.UtcNow.AddDays(-days);

                // Fallback sigur (merge pe orice EF Core): încărcăm și ștergem
                var old = await db.AuditLogs
                    .Where(x => x.EventTimeUtc < cutoff)
                    .ToListAsync(stoppingToken);

                deleted = old.Count;

                if (deleted > 0)
                {
                    db.AuditLogs.RemoveRange(old);
                    await db.SaveChangesAsync(stoppingToken);
                }

                // Log în audit că cleanup a rulat (SYSTEM)
                // Important: folosim aceeași instanță db din scope
                db.AuditLogs.Add(new Models.AuditLog
                {
                    EventTimeUtc = DateTime.UtcNow,
                    EventType = "Audit.Cleanup",
                    ActorUser = "SYSTEM",
                    ActorSid = null,
                    TargetType = "AuditLog",
                    TargetId = null,
                    Outcome = "Success",
                    Reason = null,
                    ClientIp = null,
                    UserAgent = null,
                    CorrelationId = null,
                    DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        retentionDays = days,
                        deleted = deleted
                    })
                });

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // încercăm să logăm și fail-ul, dar fără să crăpăm app-ul
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    db.AuditLogs.Add(new Models.AuditLog
                    {
                        EventTimeUtc = DateTime.UtcNow,
                        EventType = "Audit.Cleanup",
                        ActorUser = "SYSTEM",
                        ActorSid = null,
                        TargetType = "AuditLog",
                        TargetId = null,
                        Outcome = "Fail",
                        Reason = "Exception",
                        ClientIp = null,
                        UserAgent = null,
                        CorrelationId = null,
                        DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            retentionDays = days,
                            deleted = deleted,
                            error = ex.Message
                        })
                    });

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch
                {
                    // ignorăm complet dacă nici logarea nu merge
                }
            }
        }
    }
}
