using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

namespace SeifDigital.Services
{
    public class UserFileService
    {
        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;

        public UserFileService(ApplicationDbContext db, SettingsService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<List<string>> GetAllowedExtensionsAsync()
        {
            var raw = await _settings.GetStringLongAsync("AllowedUploadExtensions")
                      ?? ".pfx;.cer;.pem;.crt;.txt;.pdf";

            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim().ToLower())
                      .ToList();
        }

        public async Task<bool> IsExtensionAllowedAsync(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLower();
            if (string.IsNullOrWhiteSpace(ext)) return false;

            var allowed = await GetAllowedExtensionsAsync();
            return allowed.Contains(ext);
        }

        public async Task AddAsync(UserFile file)
        {
            file.UploadedUtc = DateTime.UtcNow;
            _db.UserFiles.Add(file);
            await _db.SaveChangesAsync();
        }
    }
}
