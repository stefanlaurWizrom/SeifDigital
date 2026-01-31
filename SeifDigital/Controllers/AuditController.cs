using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Services;
using System.Security.Principal;

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

        // ✅ Domain Admins = grup cu RID 512 => SID se termină cu "-512"
        private bool IsDomainAdmin()
        {
            try
            {
                var wi = User?.Identity as WindowsIdentity;
                if (wi?.Groups == null) return false;

                foreach (var g in wi.Groups)
                {
                    var sid = g?.Value;
                    if (!string.IsNullOrWhiteSpace(sid) && sid.EndsWith("-512"))
                        return true;
                }
            }
            catch
            {
                // dacă ceva pică, mai bine blocăm accesul decât să-l permitem
                return false;
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? actorUser,
            string? eventType,
            string? outcome,
            string? targetId,
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            // 🔒 protecție: doar Domain Admins
            if (!IsDomainAdmin())
                return Forbid();

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

            var rows = await q
                .OrderByDescending(x => x.EventTimeUtc)
                .Take(200)
                .ToListAsync();

            // păstrăm filtrele în ViewBag pentru a rămâne în UI
            ViewBag.ActorUser = actorUser;
            ViewBag.EventType = eventType;
            ViewBag.Outcome = outcome;
            ViewBag.TargetId = targetId;
            ViewBag.FromUtc = fromUtc?.ToString("yyyy-MM-ddTHH:mm");
            ViewBag.ToUtc = toUtc?.ToString("yyyy-MM-ddTHH:mm");

            return View(rows);
        }

        // =========================
        // B11: Settings (Retention)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            if (!IsDomainAdmin())
                return Forbid();

            var days = await _settings.GetIntAsync("AuditRetentionDays", 90);

            // safety clamp
            if (days < 7) days = 7;
            if (days > 3650) days = 3650;

            ViewBag.Days = days;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettingsSave(int auditRetentionDays)
        {
            if (!IsDomainAdmin())
                return Forbid();

            // safety clamp
            if (auditRetentionDays < 7) auditRetentionDays = 7;
            if (auditRetentionDays > 3650) auditRetentionDays = 3650;

            await _settings.SetIntAsync("AuditRetentionDays", auditRetentionDays);

            return RedirectToAction("Settings");
        }
    }
}
