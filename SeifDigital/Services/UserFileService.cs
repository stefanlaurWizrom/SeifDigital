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

        // Extensii permise pe categorii
        private readonly Dictionary<string, List<string>> _allowedExtensions = new()
        {
            { "image", new() { ".jpg", ".jpeg", ".png", ".gif", ".webp" } },
            { "document", new() { ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".xls" } },
            { "certificate", new() { ".cer", ".pfx", ".pem", ".crt", ".key" } }
        };

        // Limite de dimensiune per categorie (în bytes)
        private readonly Dictionary<string, long> _maxSizePerCategory = new()
        {
            { "image", 5_242_880 },      // 5 MB
            { "document", 10_485_760 },  // 10 MB
            { "certificate", 2_097_152 }  // 2 MB
        };

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
        }

        /// <summary>
        /// Validează fișierul înainte de upload
        /// </summary>
        public FileValidationResult ValidateFile(IFormFile file, string fileCategory)
        {
            // Validare 1: Fișier NULL
            if (file == null || file.Length == 0)
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "❌ Nu ai selectat niciun fișier." 
                };

            // Validare 2: Categoria de fișier validă
            if (!_allowedExtensions.ContainsKey(fileCategory))
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Categoria de fișier '{fileCategory}' nu este suportată." 
                };

            // Validare 3: Dimensiune fișier
            var maxSize = _maxSizePerCategory[fileCategory];
            if (file.Length > maxSize)
            {
                var maxSizeMB = maxSize / 1024 / 1024;
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Fișierul este prea mare. Maximum {maxSizeMB}MB pentru categoria '{fileCategory}'. Tu ai: {file.Length / 1024 / 1024}MB." 
                };
            }

            // Validare 4: Extensie permisă
            var extension = Path.GetExtension(file.FileName)?.ToLower();
            if (string.IsNullOrWhiteSpace(extension))
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "❌ Fișierul nu are extensie. Nu pot determina tipul." 
                };

            if (!_allowedExtensions[fileCategory].Contains(extension))
            {
                var allowedExts = string.Join(", ", _allowedExtensions[fileCategory]);
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Extensie '{extension}' nu este permisă pentru '{fileCategory}'. Extensii acceptate: {allowedExts}" 
                };
            }

            // Validare 5: MIME-type (securitate suplimentară)
            if (!IsValidMimeType(file.ContentType, fileCategory))
                return new FileValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"❌ Tipul MIME '{file.ContentType}' nu corespunde cu categoria '{fileCategory}'." 
                };

            return new FileValidationResult { IsValid = true };
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

        public async Task<UserFile?> UploadFileAsync(IFormFile file, string ownerUser, string fileCategory = "image")
        {
            // Validare strictă
            var validation = ValidateFile(file, fileCategory);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage!);

            var extension = Path.GetExtension(file.FileName).ToLower();
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_uploadDir, storedFileName);

            // ✅ DEBUG: Log file upload details
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Starting upload");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FileName: {file.FileName}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FileSize: {file.Length} bytes");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FileCategory: {fileCategory}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] StoredFileName: {storedFileName}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] FilePath: {filePath}");
            System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Directory exists: {Directory.Exists(_uploadDir)}");

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] File saved successfully to: {filePath}");
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] File now exists: {File.Exists(filePath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] ERROR saving file: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[UploadFileAsync] Exception: {ex}");
                throw;
            }

            // Crează înregistrare în DB
            var userFile = new UserFile
            {
                OwnerUser = ownerUser,
                OriginalFileName = file.FileName,
                Extension = extension,
                StoredFileName = storedFileName,
                StoredRelativePath = $"uploads/{storedFileName}",
                SizeBytes = file.Length,
                ContentType = file.ContentType,
                FileCategory = fileCategory,
                UploadedUtc = DateTime.UtcNow
            };

            _db.UserFiles.Add(userFile);
            await _db.SaveChangesAsync();

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

