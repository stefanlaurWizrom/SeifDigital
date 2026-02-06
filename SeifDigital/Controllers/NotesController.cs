using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;
using SeifDigital.Utils;

namespace SeifDigital.Controllers
{
    public class NotesController : Controller
    {
        private readonly UserNoteService _notes;
        private readonly AuditService _audit;

        public NotesController(UserNoteService notes, AuditService audit)
        {
            _notes = notes;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            // 2FA gate (important)
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");


            // IMPORTANT: ownerKey = email (Mac + Windows)
            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var domainUser = User?.Identity?.Name ?? "UNKNOWN";

            const int pageSize = 25;

            // Service-ul trebuie să caute după OwnerKey (nu OwnerUser)
            var (items, total) = await _notes.SearchForOwnerKeyAsync(ownerKey, q, page, pageSize);

            ViewBag.Q = q ?? "";
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            // optional debug
            ViewBag.OwnerKey = ownerKey;
            ViewBag.DomainUser = domainUser;

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string text)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");


            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var domainUser = User?.Identity?.Name ?? "UNKNOWN";

            // Add trebuie să seteze OwnerKey + păstrează OwnerUser pentru istoric
            await _notes.AddAsync(ownerKey, domainUser, text);

            _audit.Log(HttpContext, "Notes.Add", "Success",
                targetType: "UserNote",
                details: new { len = (text ?? "").Length });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");


            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            // Delete trebuie să valideze OwnerKey, nu OwnerUser
            await _notes.DeleteAsync(id, ownerKey);

            _audit.Log(HttpContext, "Notes.Delete", "Success",
                targetType: "UserNote",
                targetId: id.ToString());

            return RedirectToAction(nameof(Index));
        }
    }
}
