using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

namespace SeifDigital.Services
{
    public class UserProfileService
    {
        private readonly ApplicationDbContext _db;

        public UserProfileService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<string?> GetCachedEmailAsync(string domainUser)
        {
            if (string.IsNullOrWhiteSpace(domainUser)) return null;

            var row = await _db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DomainUser == domainUser);

            return row?.Email;
        }

        public async Task UpsertEmailAsync(string domainUser, string email, string source)
        {
            if (string.IsNullOrWhiteSpace(domainUser)) return;
            if (string.IsNullOrWhiteSpace(email)) return;

            var now = DateTime.UtcNow;

            var row = await _db.UserProfiles
                .FirstOrDefaultAsync(x => x.DomainUser == domainUser);

            if (row == null)
            {
                row = new UserProfile
                {
                    DomainUser = domainUser,
                    Email = email,
                    EmailSource = source,
                    LastVerifiedUtc = now,
                    LastSeenUtc = now
                };
                _db.UserProfiles.Add(row);
            }
            else
            {
                row.Email = email;
                row.EmailSource = source;
                row.LastSeenUtc = now;

                // doar dacă emailul vine din AD, îl considerăm "verificat"
                if (source == "AD")
                    row.LastVerifiedUtc = now;
            }

            await _db.SaveChangesAsync();
        }

        public async Task TouchSeenAsync(string domainUser)
        {
            if (string.IsNullOrWhiteSpace(domainUser)) return;

            var row = await _db.UserProfiles.FirstOrDefaultAsync(x => x.DomainUser == domainUser);
            if (row == null) return;

            row.LastSeenUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
