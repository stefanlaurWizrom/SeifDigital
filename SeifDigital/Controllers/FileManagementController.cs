using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Services;
using System.Text.Json;

namespace SeifDigital.Controllers
{
    public class FileManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SettingsService _settings;
        private readonly AuditService _audit;
        private readonly UserFileService _userFileService;

        public FileManagementController(ApplicationDbContext context, SettingsService settings, AuditService audit, UserFileService userFileService)
        {
            _context = context;
            _settings = settings;
            _audit = audit;
            _userFileService = userFileService;
        }

        // Verifică dacă userul e admin
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "1";
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
                return Forbid();

            // Preluează categoriile din settings
            var categories = await _settings.GetFileCategoriesAsync();
            var categoriesList = categories.Values.ToList();

            ViewBag.Categories = categoriesList;
            _audit.Log(HttpContext, "FileManagement.Index", "Success");

            return View();
        }

        // API: Update categorie
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateCategory([FromBody] UpdateCategoryRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "Dată invalidă" });

            try
            {
                // Citește categoriile din baza de date
                var categoriesJson = await _settings.GetStringLongAsync("FileCategories") ?? "";
                var categories = new List<Dictionary<string, object>>();

                if (!string.IsNullOrWhiteSpace(categoriesJson))
                {
                    categories = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(categoriesJson) ?? new();
                }
                else
                {
                    // Dacă nu există categorii în baza de date, inițializează cu defaults
                    categories = new List<Dictionary<string, object>>
                    {
                        new() { { "name", "image" }, { "label", "Imagini" }, { "extensions", ".jpg;.jpeg;.png;.gif;.webp" }, { "maxSizeMB", 5 } },
                        new() { { "name", "document" }, { "label", "Documente" }, { "extensions", ".txt;.doc;.docx;.pdf;.xlsx;.xls" }, { "maxSizeMB", 10 } },
                        new() { { "name", "certificate" }, { "label", "Certificate" }, { "extensions", ".cer;.pfx;.pem;.crt;.key" }, { "maxSizeMB", 2 } }
                    };
                }

                // Găsește categoria
                var categoryIndex = categories.FindIndex(c =>
                {
                    if (c.TryGetValue("name", out var name))
                    {
                        return name?.ToString() == request.Name;
                    }
                    return false;
                });

                if (categoryIndex == -1)
                    return NotFound(new { success = false, message = "Categoria nu a fost găsită" });

                // Actualizează categoria
                var updated = new Dictionary<string, object>
                {
                    { "name", request.Name },
                    { "label", request.Label },
                    { "extensions", request.Extensions },
                    { "maxSizeMB", request.MaxSizeMB }
                };

                categories[categoryIndex] = updated;

                var updatedJson = System.Text.Json.JsonSerializer.Serialize(categories);
                await _settings.SetStringLongAsync("FileCategories", updatedJson);

                // 🔄 Forțează reîncărcarea categoriilor în UserFileService
                await _userFileService.ForceCategoryRefreshAsync();

                _audit.Log(HttpContext, "FileManagement.UpdateCategory", "Success",
                    details: new { categoryName = request.Name, maxSizeMB = request.MaxSizeMB });

                return Ok(new { success = true, message = "Categoria actualizată cu succes" });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "FileManagement.UpdateCategory", "Fail",
                    reason: "Exception",
                    details: new { error = ex.Message });

                return BadRequest(new { success = false, message = "Eroare: " + ex.Message });
            }
        }

        // API: Adauga categorie nouă
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddCategory([FromBody] AddCategoryRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "Dată invalidă" });

            try
            {
                // Validare nume categorie
                if (!IsValidCategoryName(request.Name))
                    return BadRequest(new { success = false, message = "Numar de categorie invalid (doar lowercase a-z, numere, underscore)" });

                var categoriesJson = await _settings.GetStringLongAsync("FileCategories") ?? "";
                var categories = new List<Dictionary<string, object>>();

                if (!string.IsNullOrWhiteSpace(categoriesJson))
                {
                    categories = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(categoriesJson) ?? new();
                }
                else
                {
                    // Dacă nu există categorii în baza de date, inițializează cu defaults
                    categories = new List<Dictionary<string, object>>
                    {
                        new() { { "name", "image" }, { "label", "Imagini" }, { "extensions", ".jpg;.jpeg;.png;.gif;.webp" }, { "maxSizeMB", 5 } },
                        new() { { "name", "document" }, { "label", "Documente" }, { "extensions", ".txt;.doc;.docx;.pdf;.xlsx;.xls" }, { "maxSizeMB", 10 } },
                        new() { { "name", "certificate" }, { "label", "Certificate" }, { "extensions", ".cer;.pfx;.pem;.crt;.key" }, { "maxSizeMB", 2 } }
                    };
                }

                // Verifică dacă categoria deja există
                var exists = categories.Any(c =>
                {
                    if (c.TryGetValue("name", out var name))
                    {
                        return name?.ToString() == request.Name;
                    }
                    return false;
                });

                if (exists)
                    return BadRequest(new { success = false, message = "Categoria deja există" });

                // Adauga categoria nouă
                var newCategory = new Dictionary<string, object>
                {
                    { "name", request.Name },
                    { "label", request.Label },
                    { "extensions", request.Extensions },
                    { "maxSizeMB", request.MaxSizeMB }
                };

                categories.Add(newCategory);

                var updatedJson = System.Text.Json.JsonSerializer.Serialize(categories);
                await _settings.SetStringLongAsync("FileCategories", updatedJson);

                // 🔄 Forțează reîncărcarea categoriilor în UserFileService
                await _userFileService.ForceCategoryRefreshAsync();

                _audit.Log(HttpContext, "FileManagement.AddCategory", "Success",
                    details: new { categoryName = request.Name, maxSizeMB = request.MaxSizeMB });

                return Ok(new { success = true, message = "Categoria adăugată cu succes", newCategory });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "FileManagement.AddCategory", "Fail",
                    reason: "Exception",
                    details: new { error = ex.Message });

                return BadRequest(new { success = false, message = "Eroare: " + ex.Message });
            }
        }

        // API: Șterge categorie
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteCategory([FromBody] DeleteCategoryRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "Dată invalidă" });

            try
            {
                // Nu permite ștergerea categoriilor default
                if (IsDefaultCategory(request.Name))
                    return BadRequest(new { success = false, message = "Nu poți șterge categoriile default (image, document, certificate)" });

                var categoriesJson = await _settings.GetStringLongAsync("FileCategories") ?? "";
                var categories = new List<Dictionary<string, object>>();

                if (!string.IsNullOrWhiteSpace(categoriesJson))
                {
                    categories = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(categoriesJson) ?? new();
                }
                else
                {
                    // Dacă nu există categorii în baza de date, inițializează cu defaults
                    categories = new List<Dictionary<string, object>>
                    {
                        new() { { "name", "image" }, { "label", "Imagini" }, { "extensions", ".jpg;.jpeg;.png;.gif;.webp" }, { "maxSizeMB", 5 } },
                        new() { { "name", "document" }, { "label", "Documente" }, { "extensions", ".txt;.doc;.docx;.pdf;.xlsx;.xls" }, { "maxSizeMB", 10 } },
                        new() { { "name", "certificate" }, { "label", "Certificate" }, { "extensions", ".cer;.pfx;.pem;.crt;.key" }, { "maxSizeMB", 2 } }
                    };
                }

                var categoryIndex = categories.FindIndex(c =>
                {
                    if (c.TryGetValue("name", out var name))
                    {
                        return name?.ToString() == request.Name;
                    }
                    return false;
                });

                if (categoryIndex == -1)
                    return NotFound(new { success = false, message = "Categoria nu a fost găsită" });

                categories.RemoveAt(categoryIndex);

                var updatedJson = System.Text.Json.JsonSerializer.Serialize(categories);
                await _settings.SetStringLongAsync("FileCategories", updatedJson);

                // 🔄 Forțează reîncărcarea categoriilor în UserFileService
                await _userFileService.ForceCategoryRefreshAsync();

                _audit.Log(HttpContext, "FileManagement.DeleteCategory", "Success",
                    details: new { categoryName = request.Name });

                return Ok(new { success = true, message = "Categoria ștearsă cu succes" });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "FileManagement.DeleteCategory", "Fail",
                    reason: "Exception",
                    details: new { error = ex.Message });

                return BadRequest(new { success = false, message = "Eroare: " + ex.Message });
            }
        }

        private bool IsValidCategoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_]+$");
        }

        private bool IsDefaultCategory(string name)
        {
            return name == "image" || name == "document" || name == "certificate";
        }
    }

    // Request models
    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string Extensions { get; set; } = "";
        public int MaxSizeMB { get; set; }
    }

    public class AddCategoryRequest
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public string Extensions { get; set; } = "";
        public int MaxSizeMB { get; set; }
    }

    public class DeleteCategoryRequest
    {
        public string Name { get; set; } = "";
    }
}
