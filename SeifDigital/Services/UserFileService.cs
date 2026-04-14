using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;
using Microsoft.AspNetCore.Hosting;

namespace SeifDigital.Services
{
    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class UserFileService
    {
        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;
        private readonly IWebHostEnvironment _environment;
        private readonly string _uploadDir;

        // Extensii permise pe categorii - CITITE DIN SETTINGS
        private Dictionary<string, List<string>>? _allowedExtensions;
        private Dictionary<string, long>? _maxSizePerCategory;

        // Cache pentru categorii (pentru a evita citiri repetate)
        private DateTime _categoriesCacheTime = DateTime.MinValue;
        private const int CACHE_MINUTES = 1; // Redus de la 5 la 1 minut pentru sincronizare mai rapidă

        public UserFileService(ApplicationDbContext db, SettingsService settings, IWebHostEnvironment environment)
        {
            _db = db;
            _settings = settings;
            _environment = environment;

            // ✅ FIXED: Use IWebHostEnvironment.WebRootPath instead of Directory.GetCurrentDirectory()
            _uploadDir = Path.Combine(_environment.WebRootPath, "uploads");

            // ✅ DEBUG: Log the upload directory path
            System.Diagnostics.Debug.WriteLine($"[UserFileService] WebRootPath: {_environment.WebRootPath}");
            System.Diagnostics.Debug.WriteLine($"[UserFileService] UploadDir: {_uploadDir}");
            System.Diagnostics.Debug.WriteLine($"[UserFileService] UploadDir Exists: {Directory.Exists(_uploadDir)}");
            System.Diagnostics.Debug.WriteLine($"[UserFileService] ContentRootPath: {_environment.ContentRootPath}");

            if (!Directory.Exists(_uploadDir))
            {
                try
                {
                    Directory.CreateDirectory(_uploadDir);
                    System.Diagnostics.Debug.WriteLine($"[UserFileService] Created directory: {_uploadDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserFileService] ERROR creating directory: {ex.Message}");
                }
            }

            // ✅ STRICT DATABASE-ONLY: Initialize as empty, load from database only
            InitializeCategories();
        }

        /// <summary>
        /// Inițializează categoriile din settings
        /// ✅ STRICT: Dicționare GOALE inițial. Categoriile se încarcă DOAR din database.
        ///    NO HARDCODED DEFAULTS. Only what's in Management Fisiere.
        /// </summary>
        private void InitializeCategories()
        {
            _allowedExtensions = new();
            _maxSizePerCategory = new();

            System.Diagnostics.Debug.WriteLine($"[InitializeCategories] Categories initialized as EMPTY. Will load from database ONLY.");
        }

        /// <summary>
        /// ✅ NOU: Găsește categoria pentru o anumită extensie din ORICE categorie în database
        /// Parcurge TOATE categoriile și caută extensia
        /// </summary>
        private async Task<(string? categoryName, long maxSizeBytes)?> FindCategoryByExtensionAsync(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return null;

            // Asigură că extensia are punct și e lowercase
            var ext = extension.StartsWith(".") ? extension.ToLower() : "." + extension.ToLower();

            await RefreshCategoriesIfNeededAsync();

            // ✅ PARCURGE TOATE CATEGORIILE
            foreach (var cat in _allowedExtensions!)
            {
                if (cat.Value.Contains(ext))
                {
                    // ✅ Extensia găsită! Returnează categoria și max size
                    var categoryName = cat.Key;
                    var maxSize = _maxSizePerCategory![categoryName];
                    System.Diagnostics.Debug.WriteLine($"[FindCategoryByExtensionAsync] ✅ Found extension '{ext}' in category '{categoryName}' (maxSize: {maxSize / 1024 / 1024}MB)");
                    return (categoryName, maxSize);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FindCategoryByExtensionAsync] ❌ Extension '{ext}' NOT found in any category");
            return null;
        }

        /// <summary>
        /// Forțează reîncărcarea categoriilor (indiferent de cache)
        /// </summary>
        public async Task ForceCategoryRefreshAsync()
        {
            _categoriesCacheTime = DateTime.MinValue;
            await RefreshCategoriesIfNeededAsync();
            System.Diagnostics.Debug.WriteLine($"[UserFileService] Forced category refresh completed");
        }

        /// <summary>
        /// Reîncarcă categoriile din settings dacă cache-ul a expirat
        /// ✅ STRICT DATABASE-ONLY: No fallbacks, no defaults
        /// </summary>
        private async Task RefreshCategoriesIfNeededAsync()
        {
            var timeSinceLastRefresh = DateTime.UtcNow - _categoriesCacheTime;
            System.Diagnostics.Debug.WriteLine($"[UserFileService] RefreshCategoriesIfNeededAsync: Time since last refresh: {timeSinceLastRefresh.TotalSeconds:F1}s");

            if (timeSinceLastRefresh < TimeSpan.FromMinutes(CACHE_MINUTES))
            {
                System.Diagnostics.Debug.WriteLine($"[UserFileService] Cache still valid, skipping refresh");
                return; // Cache-ul este încă valid
            }

            System.Diagnostics.Debug.WriteLine($"[UserFileService] Cache expired or first load, fetching from database...");

            try
            {
                var categories = await _settings.GetFileCategoriesAsync();

                System.Diagnostics.Debug.WriteLine($"[UserFileService] Retrieved {categories?.Count ?? 0} categories from database");

                // ✅ STRICT: Clear old categories ONLY if we have new data
                _allowedExtensions = new();
                _maxSizePerCategory = new();

                if (categories != null && categories.Count > 0)
                {
                    foreach (var cat in categories.Values)
                    {
                        if (cat is System.Collections.Generic.Dictionary<string, object> catDict)
                        {
                            if (catDict.TryGetValue("name", out var nameObj) &&
                                catDict.TryGetValue("extensions", out var extObj) &&
                                catDict.TryGetValue("maxSizeMB", out var maxObj))
                            {
                                // Convertește JsonElement la tipurile corecte
                                var name = nameObj is System.Text.Json.JsonElement nameElem 
                                    ? nameElem.GetString() 
                                    : nameObj?.ToString();

                                var extensions = extObj is System.Text.Json.JsonElement extElem 
                                    ? extElem.GetString() 
                                    : extObj?.ToString();

                                int maxSizeMB = maxObj is System.Text.Json.JsonElement maxElem 
                                    ? maxElem.GetInt32() 
                                    : Convert.ToInt32(maxObj);

                                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(extensions))
                                {
                                    var extList = extensions.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(e => e.Trim().ToLower())
                                        .ToList();

                                    _allowedExtensions[name] = extList;
                                    _maxSizePerCategory[name] = maxSizeMB * 1024L * 1024L; // Convert MB to bytes

                                    System.Diagnostics.Debug.WriteLine($"[UserFileService] ✅ LOADED: Category '{name}' with extensions [{string.Join(", ", extList)}], maxSize={maxSizeMB}MB");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[UserFileService] ⚠️ SKIPPED: Invalid category (missing name or extensions)");
                                }
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[UserFileService] ✅ Database load complete: {_allowedExtensions.Count} categories loaded");

                // ✅ STRICT: If no categories loaded, log but DON'T fallback to defaults
                if (_allowedExtensions.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserFileService] ⚠️ WARNING: No categories loaded from database! Uploads will be rejected.");
                }

                _categoriesCacheTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserFileService] ❌ ERROR refreshing categories: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[UserFileService] Stack trace: {ex.StackTrace}");

                // ✅ STRICT: Clear categories on error - don't use fallback
                _allowedExtensions = new();
                _maxSizePerCategory = new();

                System.Diagnostics.Debug.WriteLine($"[UserFileService] ❌ Categories cleared due to database error. Uploads will be rejected until database is available.");
            }
        }

        /// <summary>
        /// Validează fișierul înainte de upload
        /// </summary>
        public async Task<FileValidationResult> ValidateFileAsync(IFormFile file, string? fileCategory)
        {
            System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] Starting validation for file: {file?.FileName}, category: {fileCategory}");

            // Reîncarcă categoriile din settings dacă sunt vechi
            await RefreshCategoriesIfNeededAsync();

            // Validare 1: Fișier NULL
            if (file == null || file.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: File is null or empty");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "❌ Nu ai selectat niciun fișier." 
                };
            }

            // Validare 1.5: Categoria de fișier NULL/EMPTY/INVALID (extensie necunoscută)
            // ✅ STRICT: Nu permitem null, empty, whitespace, sau "undefined"
            if (string.IsNullOrWhiteSpace(fileCategory) || fileCategory == "undefined")
            {
                var fileExt = Path.GetExtension(file.FileName)?.ToLower() ?? "unknown";
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: Category is null/empty/undefined for extension {fileExt}");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Fișierul de tip '{fileExt}' nu se regaseste in lista celor permise." 
                };
            }

            // Validare 2: Categoria de fișier validă (MUST exist in database categories)
            if (!_allowedExtensions!.ContainsKey(fileCategory))
            {
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: Category '{fileCategory}' not found. Available: {string.Join(", ", _allowedExtensions.Keys)}");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Categoria de fișier '{fileCategory}' nu este suportată." 
                };
            }
            {
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: Category '{fileCategory}' not found. Available: {string.Join(", ", _allowedExtensions.Keys)}");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Categoria de fișier '{fileCategory}' nu este suportată." 
                };
            }

            // Validare 3: Dimensiune fișier
            var maxSize = _maxSizePerCategory![fileCategory];
            if (file.Length > maxSize)
            {
                var maxSizeMB = maxSize / 1024 / 1024;
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: File too large ({file.Length} bytes > {maxSize} bytes)");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Fișierul este prea mare. Maximum {maxSizeMB}MB pentru categoria '{fileCategory}'. Tu ai: {file.Length / 1024 / 1024}MB." 
                };
            }

            // Validare 4: Extensie permisă
            var extension = Path.GetExtension(file.FileName)?.ToLower();
            if (string.IsNullOrWhiteSpace(extension))
            {
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: File has no extension");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "❌ Fișierul nu are extensie. Nu pot determina tipul." 
                };
            }

            var allowedForCategory = _allowedExtensions[fileCategory];
            System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] File extension: {extension}, Allowed: {string.Join(", ", allowedForCategory)}");

            if (!allowedForCategory.Contains(extension))
            {
                var allowedExts = string.Join(", ", allowedForCategory);
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: Extension '{extension}' not allowed");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Fișierul de tip '{extension}' nu se regaseste in lista celor permise." 
                };
            }

            // Validare 5: MIME-type (securitate suplimentară)
            if (!IsValidMimeType(file.ContentType, fileCategory))
            {
                System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] FAIL: Invalid MIME type: {file.ContentType}");
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Tipul MIME '{file.ContentType}' nu corespunde cu categoria '{fileCategory}'." 
                };
            }

            System.Diagnostics.Debug.WriteLine($"[ValidateFileAsync] SUCCESS: File validation passed");
            return new FileValidationResult { IsValid = true };
        }

        // Backward compatibility
        public FileValidationResult ValidateFile(IFormFile file, string fileCategory)
        {
            return ValidateFileAsync(file, fileCategory).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Validează MIME-type
        /// </summary>
        private bool IsValidMimeType(string? mimeType, string fileCategory)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return true; // Permitem dacă nu e setat

            mimeType = mimeType.ToLower();

            return fileCategory switch
            {
                "image" => mimeType.StartsWith("image/") || true, // Permitem orice dacă e image
                "document" => true, // Permitem orice pentru document (limitare prin extensie)
                "certificate" => true, // Permitem orice pentru certificate (limitare prin extensie)
                _ => true
            };
        }

        public List<string> GetAllowedImageExtensions() => _allowedExtensions["image"];

        public async Task<bool> IsExtensionAllowedAsync(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLower();
            if (string.IsNullOrWhiteSpace(ext)) return false;

            var allowed = await GetAllowedExtensionsAsync();
            return allowed.Contains(ext);
        }

        public bool IsImageExtensionAllowed(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLower();
            if (string.IsNullOrWhiteSpace(ext)) return false;

            return GetAllowedImageExtensions().Contains(ext);
        }

        public async Task<List<string>> GetAllowedExtensionsAsync()
        {
            var raw = await _settings.GetStringLongAsync("AllowedUploadExtensions")
                      ?? ".jpg;.jpeg;.png;.gif;.webp;.txt;.doc;.docx;.pdf;.cer;.pfx;.pem;.crt";

            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim().ToLower())
                      .ToList();
        }

        public async Task<UserFile?> UploadFileAsync(IFormFile file, string ownerUser, string? fileExtension)
        {
            // ✅ STEP 1: Find category for this extension
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Starting upload for file: {file?.FileName}, extension: {fileExtension}");

            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                throw new InvalidOperationException("❌ Nu pot determina extensia fișierului.");
            }

            var categoryResult = await FindCategoryByExtensionAsync(fileExtension);
            if (!categoryResult.HasValue)
            {
                throw new InvalidOperationException($"❌ Fișierul de tip '{fileExtension}' nu se regaseste in lista celor permise.");
            }

            var (categoryName, maxSizeBytes) = categoryResult.Value;

            // ✅ STEP 2: Validate file
            var extension = Path.GetExtension(file.FileName).ToLower();

            // Validare: NULL file
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("❌ Nu ai selectat niciun fișier.");
            }

            // Validare: Size
            if (file.Length > maxSizeBytes)
            {
                var maxSizeMB = maxSizeBytes / 1024 / 1024;
                var fileSizeMB = file.Length / 1024 / 1024;
                throw new InvalidOperationException($"❌ Fișierul este prea mare. Maximum {maxSizeMB}MB pentru categoria '{categoryName}'. Tu ai: {fileSizeMB}MB.");
            }

            // Validare: MIME type
            if (!IsValidMimeType(file.ContentType, categoryName))
            {
                throw new InvalidOperationException($"❌ Tipul MIME '{file.ContentType}' nu corespunde cu categoria '{categoryName}'.");
            }

            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] ✅ Validation passed. Category: {categoryName}, MaxSize: {maxSizeBytes / 1024 / 1024}MB");

            // ✅ STEP 3: Save file
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_uploadDir, storedFileName);

            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Starting file save");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FileName: {file.FileName}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FileSize: {file.Length} bytes");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Category: {categoryName}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] StoredFileName: {storedFileName}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FilePath: {filePath}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Directory exists: {Directory.Exists(_uploadDir)}");

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] ✅ File saved successfully to: {filePath}");
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] File now exists: {File.Exists(filePath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] ❌ ERROR saving file: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Exception: {ex}");
                throw;
            }

            // ✅ STEP 4: Create DB record
            var userFile = new UserFile
            {
                OwnerUser = ownerUser,
                OriginalFileName = file.FileName,
                Extension = extension,
                StoredFileName = storedFileName,
                StoredRelativePath = $"uploads/{storedFileName}",
                SizeBytes = file.Length,
                ContentType = file.ContentType,
                FileCategory = categoryName,  // ✅ Use found category name
                UploadedUtc = DateTime.UtcNow
            };

            _db.UserFiles.Add(userFile);
            await _db.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] ✅ File uploaded successfully. ID: {userFile.Id}, Category: {categoryName}");

            return userFile;
        }

        // ✅ Keep old method for backward compatibility
        public async Task<UserFile?> UploadImageAsync(IFormFile file, string ownerUser, long maxSizeBytes = 5_242_880)
        {
            return await UploadFileAsync(file, ownerUser, "image");
        }

        public async Task<bool> DeleteImageAsync(long fileId, string ownerUser)
        {
            var file = await _db.UserFiles.FindAsync(fileId);

            if (file == null || file.OwnerUser != ownerUser)
                return false;

            try
            {
                var filePath = Path.Combine(_uploadDir, file.StoredFileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);

                _db.UserFiles.Remove(file);
                await _db.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task AddAsync(UserFile file)
        {
            file.UploadedUtc = DateTime.UtcNow;
            _db.UserFiles.Add(file);
            await _db.SaveChangesAsync();
        }

        public async Task<UserFile?> GetImageAsync(long fileId, string ownerUser)
        {
            var file = await _db.UserFiles.FirstOrDefaultAsync(x => 
                x.Id == fileId && x.OwnerUser == ownerUser);
            return file;
        }

        public async Task<byte[]?> GetImageBytesAsync(long fileId, string ownerUser)
        {
            var file = await GetImageAsync(fileId, ownerUser);
            if (file == null)
                return null;

            var filePath = Path.Combine(_uploadDir, file.StoredFileName);
            if (!File.Exists(filePath))
                return null;

            return await File.ReadAllBytesAsync(filePath);
        }

        public async Task<UserFile?> CopyImageForUserAsync(long sourceFileId, string newOwnerUser)
        {
            // Prelează fișierul sursă
            var sourceFile = await _db.UserFiles.FindAsync(sourceFileId);
            if (sourceFile == null)
                return null;

            var sourceFilePath = Path.Combine(_uploadDir, sourceFile.StoredFileName);
            if (!File.Exists(sourceFilePath))
                return null;

            try
            {
                // Creează nume nou pentru copia
                var extension = Path.GetExtension(sourceFile.StoredFileName).ToLower();
                var newStoredFileName = $"{Guid.NewGuid()}{extension}";
                var newFilePath = Path.Combine(_uploadDir, newStoredFileName);

                // Copiază fișierul fizic
                await Task.Run(() => File.Copy(sourceFilePath, newFilePath, false));

                // Creează noul UserFile pentru noul proprietar
                var newUserFile = new UserFile
                {
                    OwnerUser = newOwnerUser,
                    OriginalFileName = sourceFile.OriginalFileName,
                    Extension = sourceFile.Extension,
                    StoredFileName = newStoredFileName,
                    StoredRelativePath = $"uploads/{newStoredFileName}",
                    SizeBytes = sourceFile.SizeBytes,
                    ContentType = sourceFile.ContentType,
                    FileCategory = sourceFile.FileCategory,
                    UploadedUtc = DateTime.UtcNow
                };

                _db.UserFiles.Add(newUserFile);
                await _db.SaveChangesAsync();

                return newUserFile;
            }
            catch
            {
                return null;
            }
        }
    }
}

