using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

namespace SeifDigital.Services
{
    public class UserNoteService
    {
        private readonly ApplicationDbContext _db;

        public UserNoteService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<(List<UserNote> Items, int TotalCount)> SearchForOwnerKeyAsync(
            string ownerKey,
            string? q,
            int page,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
                return (new List<UserNote>(), 0);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var query = _db.UserNotes
                .AsNoTracking()
                .Where(x => x.OwnerKey == ownerKey);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(x => x.Title.Contains(q) || x.Text.Contains(q));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.UpdatedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        // Compat: dacă mai există apeluri vechi
        public Task AddAsync(string ownerKey, string ownerUser, string text)
        {
            var title = DeriveTitle(text);
            return AddAsync(ownerKey, ownerUser, title, text);
        }

        public async Task AddAsync(string ownerKey, string ownerUser, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) return;

            title = NormalizeTitle(title);
            text = NormalizeText(text);
            if (string.IsNullOrWhiteSpace(text)) return;

            var note = new UserNote
            {
                OwnerKey = ownerKey,
                OwnerUser = ownerUser ?? "",
                Title = title,
                Text = text,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.UserNotes.Add(note);
            await _db.SaveChangesAsync();
        }

        public async Task<UserNote?> GetForEditAsync(long id, string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) return null;

            return await _db.UserNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);
        }

        public async Task<bool> UpdateAsync(long id, string ownerKey, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) return false;

            var note = await _db.UserNotes
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (note == null) return false;

            note.Title = NormalizeTitle(title);
            note.Text = NormalizeText(text);
            note.UpdatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task DeleteAsync(long id, string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) return;

            var n = await _db.UserNotes
                .FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == ownerKey);

            if (n == null) return;

            _db.UserNotes.Remove(n);
            await _db.SaveChangesAsync();
        }

        private static string NormalizeTitle(string? title)
        {
            title = (title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = "Fără titlu";
            if (title.Length > 255)
                title = title.Substring(0, 255);
            return title;
        }

        private static string NormalizeText(string? text)
        {
            text = (text ?? "").Trim();
            if (text.Length > 255)
                text = text.Substring(0, 255);
            return text;
        }

        private static string DeriveTitle(string? text)
        {
            text = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return "Fără titlu";

            var t = text.Length > 80 ? text.Substring(0, 80) : text;
            return NormalizeTitle(t);
        }
    }
}
