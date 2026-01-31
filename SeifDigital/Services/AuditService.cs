using Microsoft.AspNetCore.Http;
using SeifDigital.Data;
using SeifDigital.Models;
using System.Text.Json;

namespace SeifDigital.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _db;

        public AuditService(ApplicationDbContext db)
        {
            _db = db;
        }

        private static string? GetClientIp(HttpContext http)
        {
            // 1) Dacă e setat X-Forwarded-For, luăm primul IP (clientul real)
            var xff = http.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                // format tipic: "clientIp, proxy1, proxy2"
                var first = xff.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first))
                    return first;
            }

            // 2) fallback: RemoteIpAddress
            var ip = http.Connection.RemoteIpAddress?.ToString();

            // normalizează localhost IPv6
            if (ip == "::1") return "127.0.0.1";

            return ip;
        }

        private static string? Trunc(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        // =========================
        // Log cu HttpContext (request user)
        // =========================
        public void Log(
            HttpContext http,
            string eventType,
            string outcome,
            string? reason = null,
            string? targetType = null,
            string? targetId = null,
            object? details = null)
        {
            var actor = http.User?.Identity?.Name ?? "UNKNOWN";

            var userAgent = http.Request.Headers.UserAgent.ToString();
            userAgent = Trunc(userAgent, 512);

            var correlationId = Trunc(http.TraceIdentifier, 64);

            string? json = null;
            if (details != null)
                json = JsonSerializer.Serialize(details);
            json = Trunc(json, 4000); // NU e perfect, dar evită surprize (poți scoate dacă vrei MAX)

            var row = new AuditLog
            {
                EventTimeUtc = DateTime.UtcNow,
                EventType = Trunc(eventType, 64) ?? "",
                ActorUser = Trunc(actor, 256) ?? "UNKNOWN",
                ActorSid = null, // opțional
                TargetType = Trunc(targetType, 64),
                TargetId = Trunc(targetId, 64),
                Outcome = Trunc(outcome, 16) ?? "Unknown",
                Reason = Trunc(reason, 256),
                ClientIp = Trunc(GetClientIp(http), 64),
                UserAgent = userAgent,
                CorrelationId = correlationId,
                DetailsJson = json
            };

            _db.AuditLogs.Add(row);
            _db.SaveChanges();
        }

        // =========================
        // Log fără HttpContext (SYSTEM / background jobs)
        // =========================
        public void LogSystem(
            string eventType,
            string outcome,
            string? reason = null,
            string? targetType = null,
            string? targetId = null,
            object? details = null)
        {
            string? json = null;
            if (details != null)
                json = JsonSerializer.Serialize(details);
            json = Trunc(json, 4000); // idem

            var row = new AuditLog
            {
                EventTimeUtc = DateTime.UtcNow,
                EventType = Trunc(eventType, 64) ?? "",
                ActorUser = "SYSTEM",
                ActorSid = null,
                TargetType = Trunc(targetType, 64),
                TargetId = Trunc(targetId, 64),
                Outcome = Trunc(outcome, 16) ?? "Unknown",
                Reason = Trunc(reason, 256),
                ClientIp = null,
                UserAgent = null,
                CorrelationId = null,
                DetailsJson = json
            };

            _db.AuditLogs.Add(row);
            _db.SaveChanges();
        }
    }
}
