using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Services;
using SeifDigital.Utils;
using System.Text.Json;

namespace SeifDigital.Controllers
{
    public class MesajeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly EncryptionService _crypto;
        private readonly UserFileService _userFileService;

        public MesajeController(ApplicationDbContext db, AuditService audit, EncryptionService crypto, UserFileService userFileService)
        {
            _db = db;
            _audit = audit;
            _crypto = crypto;
            _userFileService = userFileService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var items = await _db.UserMessages.AsNoTracking()
                .Where(x => x.RecipientOwnerKey == ownerKey)
                .OrderByDescending(x => x.CreatedUtc)
                .ToListAsync();

            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetParolaJson(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Unauthorized(new { ok = false, message = "2FA nu este validat." });

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var msg = await _db.UserMessages.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.RecipientOwnerKey == ownerKey && x.SourceType == "Parole");

            if (msg == null)
                return NotFound(new { ok = false, message = "Mesaj inexistent." });

            try
            {
                var parola = _crypto.Decrypt(msg.DateCriptate ?? "");
                return Json(new { ok = true, parola });
            }
            catch
            {
                return BadRequest(new { ok = false, message = "Nu pot decripta parola." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetDetaliiJson(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Unauthorized(new { ok = false, message = "2FA nu este validat." });

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var msg = await _db.UserMessages.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.RecipientOwnerKey == ownerKey && x.SourceType == "Parole");

            if (msg == null)
                return NotFound(new { ok = false, message = "Mesaj inexistent." });

            try
            {
                var detalii = string.IsNullOrWhiteSpace(msg.DetaliiCriptate) ? "" : _crypto.Decrypt(msg.DetaliiCriptate);
                return Json(new { ok = true, detalii });
            }
            catch
            {
                return BadRequest(new { ok = false, message = "Nu pot decripta detaliile." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var domainUser = User?.Identity?.Name ?? "UNKNOWN";

            // emailul "real" logat (pentru audit)
            var loggedEmail = (HttpContext.Session.GetString("LoginEmail") ?? ownerKey ?? "").Trim().ToLowerInvariant();

            var msg = await _db.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.RecipientOwnerKey == ownerKey);
            if (msg == null)
            {
                TempData["AccessDenied"] = "Mesaj inexistent.";

                _audit.Log(HttpContext, "Message.Save", "Fail",
                    reason: "NotFoundOrNotRecipient",
                    targetType: "UserMessage",
                    targetId: id.ToString(),
                    details: new
                    {
                        messageId = id,
                        to = loggedEmail
                    });

                return RedirectToAction(nameof(Index));
            }

            if (msg.SourceType == "Parole")
            {
                var nou = new SeifDigital.Models.InformatieSensibila
                {
                    OwnerKey = ownerKey,
                    NumeUtilizator = domainUser,
                    TitluAplicatie = msg.TitluAplicatie ?? "",
                    UsernameSalvat = msg.UsernameSalvat ?? "",
                    DateCriptate = msg.DateCriptate,
                    DetaliiCriptate = msg.DetaliiCriptate,
                    DetaliiTokens = msg.DetaliiTokens
                };

                _db.InformatiiSensibile.Add(nou);

                // ✅ ACTUALIZAT: Copiază TOȚI fișierii cu FileType pentru noul utilizator
                if (!string.IsNullOrWhiteSpace(msg.AttachedImageFileIds))
                {
                    try
                    {
                        // Salvează mai întâi noua înregistrare pentru a avea ID
                        await _db.SaveChangesAsync();

                        // Deserializează array de {fileId, fileType} folosind JsonDocument (mai sigur)
                        using (var doc = JsonDocument.Parse(msg.AttachedImageFileIds))
                        {
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var element in root.EnumerateArray())
                                {
                                    try
                                    {
                                        // Extrage fileId și fileType
                                        long fileId = element.GetProperty("fileId").GetInt64();
                                        string fileType = element.GetProperty("fileType").GetString() ?? "image";

                                        var copiedFile = await _userFileService.CopyImageForUserAsync(fileId, ownerKey);
                                        if (copiedFile != null)
                                        {
                                            // ✅ ACTUALIZAT: Crează InformatieFisier cu FileType
                                            var informatieImagine = new SeifDigital.Models.InformatieFisier
                                            {
                                                InformatieSensibila_Id = nou.Id,
                                                UserFile_Id = copiedFile.Id,
                                                FileType = fileType,
                                                CreatedUtc = DateTime.UtcNow
                                            };
                                            _db.InformatiiImagini_New.Add(informatieImagine);
                                        }
                                    }
                                    catch
                                    {
                                        // Continuă cu următorul fișier dacă ceva merge prost
                                        continue;
                                    }
                                }
                            }
                        }
                        await _db.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignoră erori în copierea fișierelor, dar salvează secretul
                    }
                }
            }
            else if (msg.SourceType == "Notes")
            {
                var rawTitle = (msg.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(rawTitle))
                {
                    // fallback pentru mesaje vechi (care nu aveau titlu)
                    var fallback = (msg.NoteText ?? "").Trim();
                    rawTitle = fallback.Length > 80 ? fallback.Substring(0, 80) : fallback;
                }

                if (string.IsNullOrWhiteSpace(rawTitle))
                    rawTitle = "Fără titlu";
                if (rawTitle.Length > 255)
                    rawTitle = rawTitle.Substring(0, 255);

                var noteText = (msg.NoteText ?? "").Trim();
                if (noteText.Length > 255)
                    noteText = noteText.Substring(0, 255);

                var note = new SeifDigital.Models.UserNote
                {
                    OwnerKey = ownerKey,
                    OwnerUser = domainUser,
                    Title = rawTitle,
                    Text = noteText,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };

                _db.UserNotes.Add(note);
            }

            // marchează mesajul ca salvat
            msg.SavedUtc = DateTime.UtcNow;

            // un singur SaveChanges
            await _db.SaveChangesAsync();

            _audit.Log(HttpContext, "Message.Save", "Success",
                targetType: "UserMessage",
                targetId: msg.Id.ToString(),
                details: new
                {
                    sourceType = msg.SourceType,
                    messageId = msg.Id,
                    originalId = msg.OriginalId,
                    from = msg.SenderEmail,
                    to = loggedEmail,
                    savedUtc = msg.SavedUtc,
                    createdUtc = msg.CreatedUtc
                });

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);
            var loggedEmail = (HttpContext.Session.GetString("LoginEmail") ?? ownerKey ?? "").Trim().ToLowerInvariant();

            var msg = await _db.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.RecipientOwnerKey == ownerKey);
            if (msg == null)
                return RedirectToAction(nameof(Index));

            _db.UserMessages.Remove(msg);
            await _db.SaveChangesAsync();

            _audit.Log(HttpContext, "Message.Delete", "Success",
                targetType: "UserMessage",
                targetId: id.ToString(),
                details: new
                {
                    messageId = id,
                    to = loggedEmail
                });

            return RedirectToAction(nameof(Index));
        }
    }
}
