using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;
using System.Text.Json;

namespace SeifDigital.Services
{
    public class SettingsService
    {
        private readonly ApplicationDbContext _db;

        public SettingsService(ApplicationDbContext db)
        {
            _db = db;
        }

        // =========================
        // INT -> folosește coloana Value (EXISTENT)
        // =========================
        public async Task<int> GetIntAsync(string key, int defaultValue)
        {
            var row = await _db.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null || string.IsNullOrWhiteSpace(row.Value))
                return defaultValue;

            return int.TryParse(row.Value, out var n) ? n : defaultValue;
        }

        public async Task SetIntAsync(string key, int value)
        {
            var row = await _db.AppSettings
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null)
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value.ToString(),
                    UpdatedUtc = DateTime.UtcNow
                });
            }
            else
            {
                row.Value = value.ToString();
                row.UpdatedUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }

        // =========================
        // STRING (short) -> tot coloana Value (opțional, poate ajuta)
        // =========================
        public async Task<string?> GetStringAsync(string key, string? defaultValue = null)
        {
            var row = await _db.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null || string.IsNullOrWhiteSpace(row.Value))
                return defaultValue;

            return row.Value;
        }

        public async Task SetStringAsync(string key, string? value)
        {
            var row = await _db.AppSettings
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null)
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
            else
            {
                row.Value = value;
                row.UpdatedUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }

        // =========================
        // STRING LONG -> folosește ValueString (NOU, pentru B12)
        // =========================
        public async Task<string?> GetStringLongAsync(string key, string? defaultValue = null)
        {
            var row = await _db.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null || string.IsNullOrWhiteSpace(row.ValueString))
                return defaultValue;

            return row.ValueString;
        }

        public async Task SetStringLongAsync(string key, string? valueString)
        {
            var row = await _db.AppSettings
                .FirstOrDefaultAsync(x => x.Key == key);

            if (row == null)
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    ValueString = valueString,
                    UpdatedUtc = DateTime.UtcNow
                });
            }
            else
            {
                row.ValueString = valueString;
                row.UpdatedUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }

        // =========================
        // FILE CATEGORIES
        // =========================
        public async Task<Dictionary<string, Dictionary<string, object>>> GetFileCategoriesAsync()
        {
            try
            {
                var categoriesJson = await GetStringLongAsync("FileCategories");
                if (string.IsNullOrWhiteSpace(categoriesJson))
                    return GetDefaultFileCategories();

                var list = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(categoriesJson);
                var result = new Dictionary<string, Dictionary<string, object>>();

                if (list != null)
                {
                    foreach (var cat in list)
                    {
                        if (cat.TryGetValue("name", out var nameObj))
                        {
                            var name = nameObj?.ToString();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                result[name] = cat;
                            }
                        }
                    }
                }

                return result.Count > 0 ? result : GetDefaultFileCategories();
            }
            catch
            {
                return GetDefaultFileCategories();
            }
        }

        private Dictionary<string, Dictionary<string, object>> GetDefaultFileCategories()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                { "image", new Dictionary<string, object> { { "name", "image" }, { "label", "Imagini" }, { "extensions", ".jpg;.jpeg;.png;.gif;.webp" }, { "maxSizeMB", 5 } } },
                { "document", new Dictionary<string, object> { { "name", "document" }, { "label", "Documente" }, { "extensions", ".txt;.doc;.docx;.pdf;.xlsx;.xls" }, { "maxSizeMB", 10 } } },
                { "certificate", new Dictionary<string, object> { { "name", "certificate" }, { "label", "Certificate" }, { "extensions", ".cer;.pfx;.pem;.crt;.key" }, { "maxSizeMB", 2 } } }
            };
        }
    }
}
