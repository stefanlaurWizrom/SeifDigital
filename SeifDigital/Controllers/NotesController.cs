using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;

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
            var userCurent = User?.Identity?.Name ?? "UNKNOWN";

            const int pageSize = 25;

            var (items, total) = await _notes.SearchForUserAsync(userCurent, q, page, pageSize);

            ViewBag.Q = q ?? "";
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string text)
        {
            var userCurent = User?.Identity?.Name ?? "UNKNOWN";

            await _notes.AddAsync(userCurent, text);

            _audit.Log(HttpContext, "Notes.Add", "Success",
                targetType: "UserNote",
                details: new { len = (text ?? "").Length });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var userCurent = User?.Identity?.Name ?? "UNKNOWN";

            await _notes.DeleteAsync(id, userCurent);

            _audit.Log(HttpContext, "Notes.Delete", "Success",
                targetType: "UserNote",
                targetId: id.ToString());

            return RedirectToAction(nameof(Index));
        }
    }
}
