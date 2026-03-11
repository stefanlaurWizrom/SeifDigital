using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

namespace SeifDigital.Services
{
    public class UserFileService
    {
        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;
        private readonly string _uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        public UserFileService(ApplicationDbContext db, SettingsService settings)
        {
            _db = db;
            _settings = settings;
            
            if (!Directory.Exists(_uploadDir))
                Directory.CreateDirectory(_uploadDir);
        }

        public async Task<List<string>> GetAllowedExtensionsAsync()
        {
            var raw = await _settings.GetStringLongAsync("AllowedUploadExtensions")
                      ?? ".pfx;.cer;.pem;.crt;.txt;.pdf";

            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim().ToLower())
                      .ToList();
        }

        public List<string> GetAllowedImageExtensions() => new() { ".jpg", ".jpeg", ".png" };

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

        public async Task<UserFile?> UploadImageAsync(IFormFile file, string ownerUser, long maxSizeBytes = 5_242_880)
        {
            if (file == null || file.Length == 0)
                return null;

            if (file.Length > maxSizeBytes)
                throw new InvalidOperationException($"Fișierul este prea mare (max {maxSizeBytes / 1024 / 1024}MB).");

            if (!IsImageExtensionAllowed(file.FileName))
                throw new InvalidOperationException("Tip de fișier neacceptat. Doar .jpg, .jpeg, .png sunt permise.");

            var extension = Path.GetExtension(file.FileName).ToLower();
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_uploadDir, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Crează înregistrare în DB
            var userFile = new UserFile
            {
                OwnerUser = ownerUser,
                OriginalFileName = file.FileName,
                Extension = extension,
                StoredFileName = storedFileName,
                StoredRelativePath = $"uploads/{storedFileName}",  // ✅ ADAUGĂ ASTA
                SizeBytes = file.Length,
                ContentType = file.ContentType,
                UploadedUtc = DateTime.UtcNow
            };

            _db.UserFiles.Add(userFile);
            await _db.SaveChangesAsync();

            return userFile;
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
