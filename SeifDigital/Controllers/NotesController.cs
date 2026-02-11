using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;
using SeifDigital.Utils;
using SeifDigital.Data;
using Microsoft.EntityFrameworkCore;

namespace SeifDigital.Controllers
{
    public class NotesController : Controller
    {
        private readonly UserNoteService _notes;
        private readonly AuditService _audit;
        private readonly ApplicationDbContext _db;

        public NotesController(UserNoteService notes, AuditService audit, ApplicationDbContext db)
        {
            _notes = notes;
            _audit = audit;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var domainUser = User?.Identity?.Name ?? "UNKNOWN";

            const int pageSize = 25;

            var (items, total) = await _notes.SearchForOwnerKeyAsync(ownerKey, q, page, pageSize);

            ViewBag.Q = q ?? "";
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            ViewBag.OwnerKey = ownerKey;
            ViewBag.DomainUser = domainUser;

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string title, string text)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var domainUser = User?.Identity?.Name ?? "UNKNOWN";

            await _notes.AddAsync(ownerKey, domainUser, title, text);

            _audit.Log(HttpContext, "Notes.Add", "Success",
                targetType: "UserNote",
                details: new { titleLen = (title ?? "").Length, len = (text ?? "").Length });

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var note = await _notes.GetForEditAsync(id, ownerKey);
            if (note == null)
            {
                TempData["AccessDenied"] = "Nota nu există sau nu îți aparține.";
                return RedirectToAction(nameof(Index));
            }

            _audit.Log(HttpContext, "Notes.Edit.View", "Success",
                targetType: "UserNote",
                targetId: id.ToString());

            return View(note);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, string title, string text)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var ok = await _notes.UpdateAsync(id, ownerKey, title, text);
            if (!ok)
            {
                TempData["AccessDenied"] = "Nota nu există sau nu îți aparține.";
                return RedirectToAction(nameof(Index));
            }

            _audit.Log(HttpContext, "Notes.Edit.Save", "Success",
                targetType: "UserNote",
                targetId: id.ToString(),
                details: new { titleLen = (title ?? "").Length, len = (text ?? "").Length });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            await _notes.DeleteAsync(id, ownerKey);

            _audit.Log(HttpContext, "Notes.Delete", "Success",
                targetType: "UserNote",
                targetId: id.ToString());

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(long id, string recipientEmail)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var senderEmail = (HttpContext.Session.GetString("LoginEmail") ?? ownerKey ?? "").Trim();

            recipientEmail = (recipientEmail ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["AccessDenied"] = "Te rog introdu un email destinatar.";
                return RedirectToAction(nameof(Index));
            }

            var recipientExists = await _db.UserAccounts.AsNoTracking()
                .AnyAsync(x => x.Email.ToLower() == recipientEmail);

            if (!recipientExists)
            {
                TempData["AccessDenied"] = "Destinatarul nu există în aplicație.";
                return RedirectToAction(nameof(Index));
            }

            var note = await _db.UserNotes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (note == null)
            {
                TempData["AccessDenied"] = "Nota nu există sau nu îți aparține.";
                return RedirectToAction(nameof(Index));
            }

            var msg = new SeifDigital.Models.UserMessage
            {
                RecipientOwnerKey = recipientEmail,
                SenderEmail = senderEmail,
                SourceType = "Notes",
                OriginalId = note.Id,
                CreatedUtc = DateTime.UtcNow,
                Text = note.Title,
                NoteText = note.Text
            };

            _db.UserMessages.Add(msg);
            await _db.SaveChangesAsync();

            _audit.Log(HttpContext, "Message.Send", "Success",
                targetType: "UserMessage",
                targetId: msg.Id.ToString(),
                details: new
                {
                    sourceType = "Notes",
                    messageId = msg.Id,
                    originalId = id,
                    from = senderEmail,
                    to = recipientEmail,
                    createdUtc = msg.CreatedUtc
                });

            return RedirectToAction(nameof(Index));
        }
    }
}
