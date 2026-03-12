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
        private readonly UserFileService _userFileService;  // ✅ NOU

        public HomeController(
            ApplicationDbContext context, 
            EncryptionService crypto, 
            AuditService audit,
            UserFileService userFileService)  // ✅ NOU
        {
            _context = context;
            _crypto = crypto;
            _audit = audit;
            _userFileService = userFileService;  // ✅ NOU
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

        // ====== ȘTERGERE: șterge înregistrarea + fișierele atasate ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sterge(int id)
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

            var item = await _context.InformatiiSensibile
                .Include(x => x.Fisieri)
                .ThenInclude(x => x.UserFile)
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

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
            var fileCount = 0;

            // ✅ NOU: Șterge fișierele fizice din disc + din DB
            if (item.Fisieri != null && item.Fisieri.Count > 0)
            {
                // ✅ FIX: Fă copie a listei pentru a evita "Collection was modified" eroare
                var fisiersCopy = item.Fisieri.ToList();

                foreach (var fisier in fisiersCopy)
                {
                    if (fisier.UserFile != null)
                    {
                        try
                        {
                            // Șterg fișierul fizic
                            await _userFileService.DeleteImageAsync(fisier.UserFile_Id, ownerKey);
                            fileCount++;
                        }
                        catch
                        {
                            // Continuă chiar dacă ștergerea fizică eșuează
                            continue;
                        }
                    }
                }
            }

            // Șterg înregistrarea (relațiile InormatieFisier vor fi șterse automat datorită cascade delete)
            _context.InformatiiSensibile.Remove(item);
            await _context.SaveChangesAsync();

            _audit.Log(HttpContext,
                eventType: "Secret.Delete",
                outcome: "Success",
                targetType: "Secret",
                targetId: id.ToString(),
                details: new { titlu, filesDeleted = fileCount });

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

            var item = await _context.InformatiiSensibile
                .Include(x => x.Fisieri)
                .ThenInclude(x => x.UserFile)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                TempData["AccessDenied"] = "Înregistrarea nu există sau nu îți aparține.";
                return RedirectToAction("Index");
            }

            // ✅ Colectează TOȚI fișierii cu tipul lor
            var attachedFiles = new List<object>();
            if (item.Fisieri != null)
            {
                foreach (var file in item.Fisieri.Where(f => f.UserFile != null))
                {
                    attachedFiles.Add(new { fileId = file.UserFile_Id, fileType = file.FileType });
                }
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
                DetaliiTokens = item.DetaliiTokens,

                // ✅ ACTUALIZAT: Stochează fișierele cu FileType ca JSON
                AttachedImageFileIds = attachedFiles.Count > 0 
                    ? System.Text.Json.JsonSerializer.Serialize(attachedFiles)
                    : null
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
                    createdUtc = msg.CreatedUtc,
                    fileCount = attachedFiles.Count
            });
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var ownerKey = HttpContext.Session.GetString("LoginEmail");
            if (string.IsNullOrWhiteSpace(ownerKey) || HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                return RedirectToAction("Login", "Account");
            }

            var item = await _context.InformatiiSensibile
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                TempData["AccessDenied"] = "Înregistrarea nu există sau nu ai acces la ea.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new InformatieSensibilaEditViewModel
            {
                Id = item.Id,
                TitluAplicatie = item.TitluAplicatie,
                UsernameSalvat = item.UsernameSalvat
            };

            try
            {
                vm.Parola = _crypto.Decrypt(item.DateCriptate ?? "");
            }
            catch
            {
                vm.Parola = "";
            }

            try
            {
                vm.Detalii = string.IsNullOrWhiteSpace(item.DetaliiCriptate) ? "" : _crypto.Decrypt(item.DetaliiCriptate);
            }
            catch
            {
                vm.Detalii = "";
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InformatieSensibilaEditViewModel model)
        {
            var ownerKey = HttpContext.Session.GetString("LoginEmail");
            if (string.IsNullOrWhiteSpace(ownerKey) || HttpContext.Session.GetString("Status2FA") != "Validat")
            {
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var item = await _context.InformatiiSensibile
                .FirstOrDefaultAsync(x => x.Id == model.Id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                TempData["AccessDenied"] = "Înregistrarea nu există sau nu ai acces la ea.";
                return RedirectToAction(nameof(Index));
            }

            item.TitluAplicatie = (model.TitluAplicatie ?? "").Trim();
            item.UsernameSalvat = (model.UsernameSalvat ?? "").Trim();

            // Se salvează doar criptat
            item.DateCriptate = _crypto.Encrypt(model.Parola ?? "");
            item.DetaliiCriptate = string.IsNullOrWhiteSpace(model.Detalii) ? null : _crypto.Encrypt(model.Detalii);
            // Nu folosim câmp LastUpdatedUtc în DB/model (evităm schimbări de schemă).
            // Dacă vrei „Last updated” real, îl adăugăm ulterior ca coloană + migrare.
            await _context.SaveChangesAsync();

            _audit.Log(HttpContext, "Secret.Edit", "Success", "InformatieSensibila", item.Id.ToString());

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(int id, IFormFile? imageFile, string fileType = "image")
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return RedirectToAction("Login", "Account");

            string ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
            {
                TempData["Error"] = "❌ Înregistrarea nu există sau nu ai acces.";
                return RedirectToAction("Index");
            }

            if (imageFile == null || imageFile.Length == 0)
            {
                TempData["Error"] = "❌ Te rog selectează un fișier.";
                return RedirectToAction("Index");
            }

            try
            {
                // Validare strictă cu mesaj descriptiv
                var userFile = await _userFileService.UploadFileAsync(imageFile, ownerKey, fileType);

                if (userFile != null)
                {
                    // Adaugă fișier nou la colecție
                    var informatieImagine = new Models.InformatieFisier
                    {
                        InformatieSensibila_Id = id,
                        UserFile_Id = userFile.Id,
                        FileType = fileType,
                        CreatedUtc = DateTime.UtcNow
                    };

                    _context.InformatiiImagini_New.Add(informatieImagine);
                    item.LastUpdatedUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"✅ Fișier {fileType} încărcat cu succes!";
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message; // Mesaj specific de validare
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Eroare la încărcarea fișierului: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int informationId, long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Forbid();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == informationId && x.OwnerKey == ownerKey);

            if (item == null)
                return NotFound();

            // ✅ ACTUALIZAT: Șterge în ordinea corectă
            var informatieImagine = await _context.InformatiiImagini_New
                .FirstOrDefaultAsync(x => x.InformatieSensibila_Id == informationId && x.UserFile_Id == fileId);

            if (informatieImagine != null)
            {
                try
                {
                    // 1️⃣ Șterge relația din DB MAI ÎNTÂI
                    _context.InformatiiImagini_New.Remove(informatieImagine);
                    item.LastUpdatedUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // 2️⃣ După ștergerea relației, șterg fișierul fizic
                    var result = await _userFileService.DeleteImageAsync(fileId, ownerKey);

                    if (result)
                    {
                        return Ok(new { success = true, message = "✅ Fișier șters cu succes!" });
                    }
                    else
                    {
                        // Fișierul fizic nu s-a șters, dar relația a fost ștearsă - e OK
                        return Ok(new { success = true, message = "✅ Fișier șters din înregistrare!" });
                    }
                }
                catch (Exception ex)
                {
                    return BadRequest(new { success = false, message = $"❌ Eroare: {ex.Message}" });
                }
            }

            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> DownloadImage(long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Forbid();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var fileData = await _userFileService.GetImageAsync(fileId, ownerKey);
            if (fileData == null)
                return NotFound();

            var fileBytes = await _userFileService.GetImageBytesAsync(fileId, ownerKey);
            if (fileBytes == null)
                return NotFound();

            return File(fileBytes, fileData.ContentType ?? "application/octet-stream", 
                $"{Path.GetFileNameWithoutExtension(fileData.OriginalFileName)}{fileData.Extension}");
        }

        [HttpGet]
        public async Task<IActionResult> GetImagePreview(long fileId)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Forbid();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

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

        [HttpGet]
        public async Task<IActionResult> GetInformationImages(int id)
        {
            if (HttpContext.Session.GetString("Status2FA") != "Validat")
                return Forbid();

            var ownerKey = OwnerKeyHelper.GetOwnerKey(HttpContext, User.Identity?.Name);

            var item = _context.InformatiiSensibile
                .FirstOrDefault(x => x.Id == id && x.OwnerKey == ownerKey);

            if (item == null)
                return NotFound();

            // ✅ ACTUALIZAT: Citește din InformatiiImagini_New (InformatieFisier) nu din InformatiiImagini
            var imagini = await _context.InformatiiImagini_New
                .Where(x => x.InformatieSensibila_Id == id)
                .Include(x => x.UserFile)
                .OrderByDescending(x => x.CreatedUtc)
                .Select(x => new
                {
                    id = x.UserFile!.Id,
                    fileName = x.UserFile.OriginalFileName,
                    extension = x.UserFile.Extension,
                    sizeBytes = x.UserFile.SizeBytes,
                    uploadedUtc = x.UserFile.UploadedUtc,
                    previewUrl = $"/Home/GetImagePreview?fileId={x.UserFile.Id}"
                })
                .ToListAsync();

            return Json(new { success = true, images = imagini });
        }

    }
}
