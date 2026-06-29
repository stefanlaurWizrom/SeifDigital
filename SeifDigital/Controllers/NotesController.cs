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
        private readonly UserFileService _userFileService;  // ✅ NOU
        private readonly SettingsService _settings;  // ✅ NOU
        private readonly SmtpEmailSender _email;  // ✅ NOU: Email service

        public NotesController(UserNoteService notes, AuditService audit, ApplicationDbContext db, UserFileService userFileService, SettingsService settings, SmtpEmailSender email)
        {
            _notes = notes;
            _audit = audit;
            _db = db;
            _userFileService = userFileService;  // ✅ NOU
            _settings = settings;  // ✅ NOU
            _email = email;  // ✅ NOU: Email service
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

            // ✅ NOU: Include fișierele notei
            var note = await _db.UserNotes
                .Include(x => x.Fisieri)
                .ThenInclude(x => x.UserFile)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (note == null)
            {
                TempData["AccessDenied"] = "Nota nu există sau nu îți aparține.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ NOU: Colectează TOȚI fișierii cu tipul lor
            var attachedFiles = new List<object>();
            if (note.Fisieri != null)
            {
                foreach (var file in note.Fisieri.Where(f => f.UserFile != null))
                {
                    attachedFiles.Add(new { fileId = file.UserFile_Id, fileType = file.FileType });
                }
            }

            var msg = new SeifDigital.Models.UserMessage
            {
                RecipientOwnerKey = recipientEmail,
                SenderEmail = senderEmail,
                SourceType = "Notes",
                OriginalId = note.Id,
                CreatedUtc = DateTime.UtcNow,
                Text = note.Title,
                NoteText = note.Text,
                // ✅ NOU: Stochează fișierele cu FileType ca JSON
                AttachedImageFileIds = attachedFiles.Count > 0 
                    ? System.Text.Json.JsonSerializer.Serialize(attachedFiles)
                    : null
            };

            _db.UserMessages.Add(msg);
            await _db.SaveChangesAsync();

            // ✅ NOU: Trimite email către destinatar
            try
            {
                string subject = "Mesaj nou în WizVault";
                string body = $@"Ati primit un mesaj nou in Wizvault

https://wizvault.wizpro.ro/";

                _email.Send(recipientEmail, subject, body);

                _audit.Log(HttpContext, "Message.Send.Email", "Success",
                    targetType: "UserMessage",
                    targetId: msg.Id.ToString(),
                    details: new { to = recipientEmail, subject });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "Message.Send.Email", "Error",
                    targetType: "UserMessage",
                    targetId: msg.Id.ToString(),
                    reason: "SmtpError",
                    details: new { to = recipientEmail, error = ex.Message });
                // ⚠️ Nu aruncăm eroare - mesajul a fost salvat în DB oricum
            }

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
                    createdUtc = msg.CreatedUtc,
                    fileCount = attachedFiles.Count  // ✅ NOU
                });

            return RedirectToAction(nameof(Index));
        }

        // ✅ NOU: UPLOAD fișier pentru o notă
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(long id, IFormFile? imageFile, string? fileExtension)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Json(new { ok = false, message = "❌ Sesiune expirat. Te rog reîncarcă pagina." });

            // ✅ VALIDATION: fileExtension must NOT be null/empty
            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                return Json(new { ok = false, message = "❌ Nu pot determina extensia fișierului." });
            }

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var note = _db.UserNotes
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (note == null)
                return Json(new { ok = false, message = "❌ Nota nu există sau nu ai acces." });

            if (imageFile == null || imageFile.Length == 0)
                return Json(new { ok = false, message = "❌ Te rog selectează un fișier." });

            try
            {
                // ✅ Trimite EXTENSION la service, NU categoria
                var userFile = await _userFileService.UploadFileAsync(imageFile, ownerKey, fileExtension);

                if (userFile != null)
                {
                    // Adaugă fișier nou la colecție
                    var noteFisier = new Models.NoteFisier
                    {
                        UserNote_Id = id,
                        UserFile_Id = userFile.Id,
                        FileType = userFile.FileCategory,  // ✅ Use category from userFile
                        CreatedUtc = DateTime.UtcNow
                    };

                    _db.NoteFisieri.Add(noteFisier);
                    note.UpdatedUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    _audit.Log(HttpContext,
                        eventType: "File.Upload",
                        outcome: "Success",
                        targetType: "UserFile",
                        targetId: userFile.Id.ToString(),
                        details: new { noteId = id, fileName = imageFile.FileName, fileExtension, category = userFile.FileCategory, fileSize = imageFile.Length });

                    return Json(new { ok = true, message = $"✅ Fișier încărcat cu succes!", fileId = userFile.Id });
                }

                return Json(new { ok = false, message = "❌ Eroare: Fișierul nu a putut fi salvat." });
            }
            catch (InvalidOperationException ex)
            {
                // Mesaj specific de validare (din UserFileService)
                _audit.Log(HttpContext,
                    eventType: "File.Upload",
                    outcome: "Fail",
                    reason: "ValidationError",
                    targetType: "UserFile",
                    targetId: id.ToString(),
                    details: new { fileName = imageFile?.FileName, fileExtension, error = ex.Message });

                return Json(new { ok = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext,
                    eventType: "File.Upload",
                    outcome: "Fail",
                    reason: "UnexpectedException",
                    targetType: "UserFile",
                    targetId: id.ToString(),
                    details: new { fileName = imageFile?.FileName, fileExtension, error = ex.Message });

                return Json(new { ok = false, message = $"❌ Eroare la încărcarea fișierului: {ex.Message}" });
            }
        }

        // ✅ NOU: ȘTERGEFișier de la o notă
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(long noteId, long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Json(new { success = false, message = "❌ Sesiune expirat. Te rog reîncarcă pagina." });

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var note = _db.UserNotes
                .FirstOrDefault(x => x.Id == noteId && x.OwnerKey == ownerKey);

            if (note == null)
                return Json(new { success = false, message = "❌ Nota nu există sau nu ai acces." });

            // ✅ Găsește și șterge relația
            var noteFisier = await _db.NoteFisieri
                .FirstOrDefaultAsync(x => x.UserNote_Id == noteId && x.UserFile_Id == fileId);

            if (noteFisier != null)
            {
                try
                {
                    // 1️⃣ Șterge relația din DB MAI ÎNTÂI
                    _db.NoteFisieri.Remove(noteFisier);
                    note.UpdatedUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    // 2️⃣ După ștergerea relației, șterg fișierul fizic
                    var result = await _userFileService.DeleteImageAsync(fileId, ownerKey);

                    if (result)
                    {
                        return Json(new { success = true, message = "✅ Fișier șters cu succes!" });
                    }
                    else
                    {
                        // Fișierul fizic nu s-a șters, dar relația a fost ștearsă - e OK
                        return Json(new { success = true, message = "✅ Fișier șters din notă!" });
                    }
                }
                catch (Exception ex)
                {
                    _audit.Log(HttpContext, "File.Delete", "Error", targetId: $"{noteId}_{fileId}", reason: ex.Message);
                    return Json(new { success = false, message = $"❌ Eroare: {ex.Message}" });
                }
            }

            return Json(new { success = false, message = "❌ Fișierul nu a fost găsit." });
        }

        // ✅ NOU: DESCARCĂ fișier de la o notă
        [HttpGet]
        public async Task<IActionResult> DownloadImage(long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Unauthorized();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var fileData = await _userFileService.GetImageAsync(fileId, ownerKey);
            if (fileData == null)
                return NotFound();

            var fileBytes = await _userFileService.GetImageBytesAsync(fileId, ownerKey);
            if (fileBytes == null)
                return NotFound();

            return File(fileBytes, fileData.ContentType ?? "application/octet-stream", 
                $"{Path.GetFileNameWithoutExtension(fileData.OriginalFileName)}{fileData.Extension}");
        }

        // ✅ NOU: PREVIZUALIZARE fișier de la o notă
        [HttpGet]
        public async Task<IActionResult> GetImagePreview(long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Unauthorized();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var fileData = await _userFileService.GetImageAsync(fileId, ownerKey);
            if (fileData == null)
                return NotFound();

            var fileBytes = await _userFileService.GetImageBytesAsync(fileId, ownerKey);
            if (fileBytes == null)
                return NotFound();

            // Determină content-type pe baza extensiei dacă nu e stocat
            string contentType = fileData.ContentType ?? "image/jpeg";

            if (string.IsNullOrEmpty(fileData.ContentType))
            {
                contentType = fileData.Extension?.ToLower() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
            }

            return File(fileBytes, contentType);
        }

        // ✅ NOU: LISTA fișiere atasate la o notă (JSON)
        [HttpGet]
        public async Task<IActionResult> GetNoteImages(long id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Json(new { success = false, message = "❌ Sesiune expirat.", images = new List<object>() });

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User?.Identity?.Name);

            var note = _db.UserNotes
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (note == null)
                return Json(new { success = false, message = "❌ Nota nu există.", images = new List<object>() });

            try
            {
                // ✅ Citește din NoteFisieri
                var imagini = await _db.NoteFisieri
                    .Where(x => x.UserNote_Id == id)
                    .Include(x => x.UserFile)
                    .OrderByDescending(x => x.CreatedUtc)
                    .Select(x => new
                    {
                        id = x.UserFile!.Id,
                        fileName = x.UserFile.OriginalFileName,
                        extension = x.UserFile.Extension,
                        sizeBytes = x.UserFile.SizeBytes,
                        uploadedUtc = x.UserFile.UploadedUtc,
                        fileType = x.FileType,
                        previewUrl = $"/Notes/GetImagePreview?fileId={x.UserFile.Id}"
                    })
                    .ToListAsync();

                return Json(new { success = true, images = imagini });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "GetNoteImages", "Error", targetId: id.ToString(), reason: ex.Message);
                return Json(new { success = false, message = $"❌ Eroare: {ex.Message}", images = new List<object>() });
            }
        }

        // ✅ NOU: API Endpoint - Categorii permise pentru upload
        [HttpGet]
        public async Task<IActionResult> GetFileCategoriesHtml()
        {
            try
            {
                var categories = await _settings.GetFileCategoriesAsync();
                if (categories == null || categories.Count == 0)
                {
                    return Json(new { html = "📸 Imagini (JPG, PNG, GIF, WEBP) - Max 5MB | 📄 Documente (TXT, DOC, DOCX, PDF, XLSX) - Max 10MB | 🔐 Certificate (CER, PFX, PEM, CRT, KEY) - Max 2MB" });
                }

                // Construiește HTML cu fiecare categorie
                var sb = new System.Text.StringBuilder();
                foreach (var cat in categories.Values)
                {
                    if (cat.TryGetValue("label", out var labelObj) &&
                        cat.TryGetValue("extensions", out var extObj) &&
                        cat.TryGetValue("maxSizeMB", out var maxObj))
                    {
                        var label = labelObj?.ToString() ?? "";
                        var extensions = extObj?.ToString() ?? "";
                        var maxSizeMB = Convert.ToInt32(maxObj);

                        // Format: 📸 Imagini (JPG, PNG, GIF, WEBP) - Max 5MB
                        var extList = extensions.Split(';', System.StringSplitOptions.RemoveEmptyEntries)
                            .Select(e => e.Trim().ToUpper().TrimStart('.'))
                            .ToList();
                        var extStr = string.Join(", ", extList);

                        // Emoji selector
                        var emoji = cat.TryGetValue("name", out var nameObj) ? nameObj?.ToString() switch
                        {
                            "image" => "📸",
                            "document" => "📄",
                            "certificate" => "🔐",
                            _ => "📁"
                        } : "📁";

                        sb.Append($"{emoji} {label} ({extStr}) - Max {maxSizeMB}MB | ");
                    }
                }

                // Șterge " | " final
                string html = sb.ToString().TrimEnd(' ', '|').Trim();
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = "📸 Imagini (JPG, PNG, GIF, WEBP) - Max 5MB | 📄 Documente (TXT, DOC, DOCX, PDF, XLSX) - Max 10MB | 🔐 Certificate (CER, PFX, PEM, CRT, KEY) - Max 2MB";
                }

                return Json(new { html = html });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "GetFileCategoriesHtml", "Error", reason: ex.Message);
                return Json(new { html = "📸 Imagini (JPG, PNG, GIF, WEBP) - Max 5MB | 📄 Documente (TXT, DOC, DOCX, PDF, XLSX) - Max 10MB | 🔐 Certificate (CER, PFX, PEM, CRT, KEY) - Max 2MB" });
            }
        }

        /// <summary>
        /// ✅ NOU: Returnează structura categoriilor pentru frontend (JSON)
        /// Folosit de JavaScript pentru a determina fileType dinamic
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFileCategoriesJson()
        {
            try
            {
                var categories = await _settings.GetFileCategoriesAsync();

                var result = new List<object>();

                // Fallback defaults dacă nu găsim categorii
                if (categories == null || categories.Count == 0)
                {
                    result.Add(new { name = "image", extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" } });
                    result.Add(new { name = "document", extensions = new[] { ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".xls" } });
                    result.Add(new { name = "certificate", extensions = new[] { ".cer", ".pfx", ".pem", ".crt", ".key" } });
                    return Json(new { categories = result });
                }

                foreach (var cat in categories.Values)
                {
                    if (cat.TryGetValue("name", out var nameObj) &&
                        cat.TryGetValue("extensions", out var extObj))
                    {
                        var name = nameObj?.ToString();
                        var extensionsStr = extObj?.ToString() ?? "";

                        // Parse extensions: ".jpg;.jpeg;.png" -> [".jpg", ".jpeg", ".png"]
                        var extensions = extensionsStr
                            .Split(';', System.StringSplitOptions.RemoveEmptyEntries)
                            .Select(e => e.Trim().ToLower())
                            .ToList();

                        result.Add(new
                        {
                            name = name,
                            extensions = extensions
                        });
                    }
                }

                // Dacă nu s-a adăugat nimic, returnează defaults
                if (result.Count == 0)
                {
                    result.Add(new { name = "image", extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" } });
                    result.Add(new { name = "document", extensions = new[] { ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".xls" } });
                    result.Add(new { name = "certificate", extensions = new[] { ".cer", ".pfx", ".pem", ".crt", ".key" } });
                }

                return Json(new { categories = result });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "GetFileCategoriesJson", "Error", reason: ex.Message);

                // Return fallback defaults on any error
                var fallback = new List<object>
                {
                    new { name = "image", extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" } },
                    new { name = "document", extensions = new[] { ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".xls" } },
                    new { name = "certificate", extensions = new[] { ".cer", ".pfx", ".pem", ".crt", ".key" } }
                };
                return Json(new { categories = fallback });
            }
        }
    }
}
