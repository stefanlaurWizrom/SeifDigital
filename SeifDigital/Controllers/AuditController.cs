using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Services;
using System.Text;

namespace SeifDigital.Controllers
{
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;

        public AuditController(ApplicationDbContext db, SettingsService settings)
        {
            _db = db;
            _settings = settings;
        }

        private bool IsAuditAdmin()
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return false;

            return HttpContext.Session.GetString("IsAdmin") == "1";
        }

        private IActionResult NoAccess()
        {
            TempData["AccessDenied"] = "Nu aveți acces la această secțiune a aplicației.";
            return RedirectToAction("Index", "Home");
        }

        private IQueryable<SeifDigital.Models.AuditLog> BuildQuery(
            string? actorUser,
            string? eventType,
            string? outcome,
            string? targetId,
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            var q = _db.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(actorUser))
                q = q.Where(x => x.ActorUser.Contains(actorUser));

            if (!string.IsNullOrWhiteSpace(eventType))
                q = q.Where(x => x.EventType.Contains(eventType));

            if (!string.IsNullOrWhiteSpace(outcome))
                q = q.Where(x => x.Outcome == outcome);

            if (!string.IsNullOrWhiteSpace(targetId))
                q = q.Where(x => x.TargetId == targetId);

            if (fromUtc.HasValue)
                q = q.Where(x => x.EventTimeUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                q = q.Where(x => x.EventTimeUtc <= toUtc.Value);

            return q;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? actorUser,
            string? eventType,
            string? outcome,
            string? targetId,
            DateTime? fromUtc,
            DateTime? toUtc,
            int page = 1)
        {
            if (!IsAuditAdmin())
                return NoAccess();

            const int pageSize = 25;

            var retentionDays = await _settings.GetIntAsync("AuditRetentionDays", 90);
            if (retentionDays < 7) retentionDays = 7;
            if (retentionDays > 3650) retentionDays = 3650;

            var q = BuildQuery(actorUser, eventType, outcome, targetId, fromUtc, toUtc);

            var total = await q.CountAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var rows = await q
                .OrderByDescending(x => x.EventTimeUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.ActorUser = actorUser;
            ViewBag.EventType = eventType;
            ViewBag.Outcome = outcome;
            ViewBag.TargetId = targetId;
            ViewBag.FromUtc = fromUtc?.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.ToUtc = toUtc?.ToString("yyyy-MM-ddTHH:mm");

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = totalPages;

            ViewBag.RetentionDays = retentionDays;

            return View(rows);
        }

        // =========================
        // Export CSV (max 5000 rows)
        // =========================
        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? actorUser,
            string? eventType,
            string? outcome,
            string? targetId,
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            if (!IsAuditAdmin())
                return NoAccess();

            var q = BuildQuery(actorUser, eventType, outcome, targetId, fromUtc, toUtc);

            var rows = await q
                .OrderByDescending(x => x.EventTimeUtc)
                .Take(5000)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("EventTimeUtc,EventType,ActorUser,TargetType,TargetId,Outcome,ClientIp,CorrelationId,Reason,UserAgent,DetailsJson");

            string esc(string? s)
            {
                s ??= "";
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }

            foreach (var x in rows)
            {
                sb.Append(esc(x.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"))).Append(",");
                sb.Append(esc(x.EventType)).Append(",");
                sb.Append(esc(x.ActorUser)).Append(",");
                sb.Append(esc(x.TargetType)).Append(",");
                sb.Append(esc(x.TargetId)).Append(",");
                sb.Append(esc(x.Outcome)).Append(",");
                sb.Append(esc(x.ClientIp)).Append(",");
                sb.Append(esc(x.CorrelationId)).Append(",");
                sb.Append(esc(x.Reason)).Append(",");
                sb.Append(esc(x.UserAgent)).Append(",");
                sb.Append(esc(x.DetailsJson)).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"audit_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }

        // =========================
        // Export "PDF" via print-friendly HTML
        // (user prints to PDF in browser)
        // =========================
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            string? actorUser,
            string? eventType,
            string? outcome,
            string? targetId,
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            if (!IsAuditAdmin())
                return NoAccess();

            var q = BuildQuery(actorUser, eventType, outcome, targetId, fromUtc, toUtc);

            var rows = await q
                .OrderByDescending(x => x.EventTimeUtc)
                .Take(1000)
                .ToListAsync();

            ViewBag.Filter = new
            {
                actorUser,
                eventType,
                outcome,
                targetId,
                fromUtc,
                toUtc
            };

            return View("ExportPdf", rows);
        }

        // =========================
        // Settings (Retention)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            if (!IsAuditAdmin())
                return NoAccess();

            var days = await _settings.GetIntAsync("AuditRetentionDays", 90);
            if (days < 7) days = 7;
            if (days > 3650) days = 3650;

            ViewBag.Days = days;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettingsSave(int auditRetentionDays)
        {
            if (!IsAuditAdmin())
                return NoAccess();

            if (auditRetentionDays < 7) auditRetentionDays = 7;
            if (auditRetentionDays > 3650) auditRetentionDays = 3650;

            await _settings.SetIntAsync("AuditRetentionDays", auditRetentionDays);

            return RedirectToAction("Settings");
        }
    }
}
