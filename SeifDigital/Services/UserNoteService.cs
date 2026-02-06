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
                query = query.Where(x => x.Text.Contains(q));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.UpdatedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task AddAsync(string ownerKey, string ownerUser, string text)
        {
            if (string.IsNullOrWhiteSpace(ownerKey)) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            text = text.Trim();
            if (text.Length > 255) text = text.Substring(0, 255);

            var note = new UserNote
            {
                OwnerKey = ownerKey,
                OwnerUser = ownerUser ?? "",
                Text = text,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.UserNotes.Add(note);
            await _db.SaveChangesAsync();
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
    }
}
