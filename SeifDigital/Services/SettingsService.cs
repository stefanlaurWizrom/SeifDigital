using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

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
    }
}
