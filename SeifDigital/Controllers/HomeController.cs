using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using SeifDigital.Models;
using SeifDigital.Data;
using SeifDigital.Services;
using System.Text;
using System.Text.RegularExpressions;
using SeifDigital.Utils;

namespace SeifDigital.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly EncryptionService _crypto;

        public HomeController(ApplicationDbContext context, EncryptionService crypto, AuditService audit)
        {
            _context = context;
            _crypto = crypto;
            _audit = audit;
        }

        // Pagina principală (căutare + paginare 25/pg)
        [HttpGet]
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            // Verificare 2FA
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");


            // IMPORTANT: ownerKey = email (Mac + Windows) sau fallback
            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);
            const int pageSize = 25;

            if (string.IsNullOrWhiteSpace(q))
            {
                var baseQuery = _context.InformatiiSensibile
                    .AsNoTracking()
                    .Where(x => x.OwnerKey == ownerKey);

                var totalCount = await baseQuery.CountAsync();

                var totalPages = (totalCount + pageSize - 1) / pageSize;
                if (totalPages == 0) totalPages = 1;
                if (page < 1) page = 1;
                if (page > totalPages) page = totalPages;

                var items = await baseQuery
                    .OrderByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var modelNoSearch = new InformatieSensibilaListViewModel
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages
                };

                ViewBag.Q = "";
                return View(modelNoSearch);
            }

            // Search path: use FREETEXT (safer for user input)
            var qTrimmed = q.Trim();
            var skip = (page - 1) * pageSize;
            int totalCountSearch = 0;

            var conn = _context.Database.GetDbConnection();
            try
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var countCmd = conn.CreateCommand();
                countCmd.CommandText =
                    "SELECT COUNT(1) FROM dbo.InformatiiSensibile " +
                    "WHERE OwnerKey = @owner AND FREETEXT((TitluAplicatie, UsernameSalvat, DetaliiTokens), @term);";

                var pOwner = countCmd.CreateParameter();
                pOwner.ParameterName = "@owner";
                pOwner.Value = ownerKey;
                countCmd.Parameters.Add(pOwner);

                var pTerm = countCmd.CreateParameter();
                pTerm.ParameterName = "@term";
                pTerm.Value = qTrimmed;
                countCmd.Parameters.Add(pTerm);

                var scalar = await countCmd.ExecuteScalarAsync();
                totalCountSearch = Convert.ToInt32(scalar ?? 0);
            }
            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                    await conn.CloseAsync();
            }

            var totalPagesSearch = (totalCountSearch + pageSize - 1) / pageSize;
            if (totalPagesSearch == 0) totalPagesSearch = 1;
            if (page < 1) page = 1;
            if (page > totalPagesSearch) page = totalPagesSearch;

            var itemsSearch = await _context.InformatiiSensibile
                .FromSqlInterpolated($@"
                    SELECT *
                    FROM dbo.InformatiiSensibile
                    WHERE OwnerKey = {ownerKey}
                      AND FREETEXT((TitluAplicatie, UsernameSalvat, DetaliiTokens), {qTrimmed})
                    ORDER BY Id DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY")
                .AsNoTracking()
                .ToListAsync();

            var model = new InformatieSensibilaListViewModel
            {
                Items = itemsSearch,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCountSearch,
                TotalPages = totalPagesSearch
            };

            ViewBag.Q = q ?? "";
            return View(model);
        }

        // ===== Privacy page (footer link) =====
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }


        // Salvare date (parola se salvează criptată)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SalveazaDateSensibile(string titlu, string usernameSalvat, string parolaClara, string? detalii)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Create",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: null,
                    details: new { titlu });

                return RedirectToAction("Login", "Account");

            }

            if (string.IsNullOrWhiteSpace(titlu) ||
                string.IsNullOrWhiteSpace(usernameSalvat) ||
                string.IsNullOrWhiteSpace(parolaClara))
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Create",
                    outcome: "Fail",
                    reason: "Validation",
                    targetType: "Secret",
                    targetId: null,
                    details: new { titlu, usernameSalvat });

                return RedirectToAction("Index");
            }

            // IMPORTANT: ownerKey = email (Mac + Windows) sau fallback
            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            // 1) Criptăm parola
            string parolaCriptata = _crypto.Encrypt(parolaClara);

            // 2) Criptăm detaliile (dacă există)
            string? detaliiCriptate = null;
            if (!string.IsNullOrWhiteSpace(detalii))
                detaliiCriptate = _crypto.Encrypt(detalii);

            // 2b) Tokeni pentru căutare (fără text complet)
            string? detaliiTokens = null;
            if (!string.IsNullOrWhiteSpace(detalii))
                detaliiTokens = GenerateSearchTokens(detalii);

            // 3) Construim rândul pentru SQL
            var nou = new InformatieSensibila
            {
                OwnerKey = ownerKey,                          // CHEIA UNICĂ PENTRU VAULT
                NumeUtilizator = User.Identity?.Name ?? "",   // păstrăm pentru istoric/audit (optional)
                TitluAplicatie = titlu,
                UsernameSalvat = usernameSalvat,
                DateCriptate = parolaCriptata,
                DetaliiCriptate = detaliiCriptate,
                DetaliiTokens = detaliiTokens
            };

            _context.InformatiiSensibile.Add(nou);
            _context.SaveChanges();

            _audit.Log(HttpContext,
                eventType: "Secret.Create",
                outcome: "Success",
                targetType: "Secret",
                targetId: nou.Id.ToString(),
                details: new { titlu, usernameSalvat, hasDetails = !string.IsNullOrWhiteSpace(detalii) });

            return RedirectToAction("Index");
        }

        // helper în același controller
        private static string GenerateSearchTokens(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var lower = input.ToLowerInvariant();
            lower = Regex.Replace(lower, @"[^\p{L}\p{Nd}\s]", " ");

            var parts = lower.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .Where(p => p.Length >= 2)
                             .Distinct();

            return string.Join(' ', parts);
        }

        // ====== AJAX: returnează parola decriptată ca JSON ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetParolaJson(int id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: id.ToString());

                return Unauthorized(new { ok = false, message = "2FA nu este validat." });
            }

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "NotFoundOrNotOwner",
                    targetType: "Secret",
                    targetId: id.ToString());

                return NotFound(new { ok = false, message = "Nu găsesc înregistrarea." });
            }

            try
            {
                string parolaClara = _crypto.Decrypt(item.DateCriptate ?? "");

                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Success",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie });

                return Json(new { ok = true, parola = parolaClara });
            }
            catch
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewPassword",
                    outcome: "Fail",
                    reason: "DecryptError",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie });

                return BadRequest(new { ok = false, message = "Nu pot decripta această înregistrare." });
            }
        }

        // ====== AJAX: returnează detaliile decriptate ca JSON ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetDetaliiJson(int id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewDetails",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: id.ToString());

                return Unauthorized(new { ok = false, message = "2FA nu este validat." });
            }

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewDetails",
                    outcome: "Fail",
                    reason: "NotFoundOrNotOwner",
                    targetType: "Secret",
                    targetId: id.ToString());

                return NotFound(new { ok = false, message = "Nu găsesc înregistrarea." });
            }

            try
            {
                string detalii = "";
                if (!string.IsNullOrWhiteSpace(item.DetaliiCriptate))
                    detalii = _crypto.Decrypt(item.DetaliiCriptate);

                _audit.Log(HttpContext,
                    eventType: "Secret.ViewDetails",
                    outcome: "Success",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie, hasDetails = !string.IsNullOrWhiteSpace(detalii) });

                return Json(new { ok = true, detalii });
            }
            catch
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.ViewDetails",
                    outcome: "Fail",
                    reason: "DecryptError",
                    targetType: "Secret",
                    targetId: id.ToString(),
                    details: new { titlu = item.TitluAplicatie });

                return BadRequest(new { ok = false, message = "Nu pot decripta detaliile." });
            }
        }

        // ====== ȘTERGERE: șterge doar înregistrarea ownerKey curent ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Sterge(int id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Delete",
                    outcome: "Fail",
                    reason: "2FA_NotValidated",
                    targetType: "Secret",
                    targetId: id.ToString());

                return RedirectToAction("Login", "Account");

            }

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                _audit.Log(HttpContext,
                    eventType: "Secret.Delete",
                    outcome: "Fail",
                    reason: "NotFoundOrNotOwner",
                    targetType: "Secret",
                    targetId: id.ToString());

                return RedirectToAction("Index");
            }

            var titlu = item.TitluAplicatie;

            _context.InformatiiSensibile.Remove(item);
            _context.SaveChanges();

            _audit.Log(HttpContext,
                eventType: "Secret.Delete",
                outcome: "Success",
                targetType: "Secret",
                targetId: id.ToString(),
                details: new { titlu });

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSecret(int id, string recipientEmail)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);
            string senderEmail = (HttpContext.Session.GetString("LoginEmail") ?? ownerKey ?? "").Trim();

            recipientEmail = (recipientEmail ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["AccessDenied"] = "Te rog introdu un email destinatar.";
                return RedirectToAction("Index");
            }

            var recipientExists = await _context.UserAccounts.AsNoTracking()
                .AnyAsync(x => x.Email.ToLower() == recipientEmail);

            if (!recipientExists)
            {
                TempData["AccessDenied"] = "Destinatarul nu există în aplicație.";
                return RedirectToAction("Index");
            }

            var item = await _context.InformatiiSensibile.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                TempData["AccessDenied"] = "Înregistrarea nu există sau nu îți aparține.";
                return RedirectToAction("Index");
            }

            var msg = new SeifDigital.Models.UserMessage
            {
                RecipientOwnerKey = recipientEmail,
                SenderEmail = senderEmail,
                SourceType = "Parole",
                OriginalId = item.Id,
                CreatedUtc = DateTime.UtcNow,

                TitluAplicatie = item.TitluAplicatie,
                UsernameSalvat = item.UsernameSalvat,
                DateCriptate = item.DateCriptate,
                DetaliiCriptate = item.DetaliiCriptate,
                DetaliiTokens = item.DetaliiTokens
            };

            _context.UserMessages.Add(msg);
            await _context.SaveChangesAsync();

            _audit.Log(HttpContext, "Message.Send", "Success",
            targetType: "UserMessage",
            targetId: msg.Id.ToString(),
            details: new
            {
                    sourceType = "Parole",
                    messageId = msg.Id,
                    originalId = id,
                    from = senderEmail,
                    to = recipientEmail,
                    createdUtc = msg.CreatedUtc
            });
            return RedirectToAction("Index");
        }

    }
}
