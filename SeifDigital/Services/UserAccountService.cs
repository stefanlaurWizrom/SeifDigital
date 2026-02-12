using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;
using System.Security.Cryptography;

namespace SeifDigital.Services
{
    public class UserAccountService
    {
        private readonly ApplicationDbContext _db;

        public UserAccountService(ApplicationDbContext db)
        {
            _db = db;
        }

        public static string NormalizeEmail(string email)
            => (email ?? "").Trim().ToLowerInvariant();

        public static bool IsAllowedEmail(string email)
        {
            email = NormalizeEmail(email);
            return email.EndsWith("@wizrom.ro", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<UserAccount?> FindByEmailAsync(string email)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email)) return null;

            return await _db.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            email = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(email)) return false;

            return await _db.UserAccounts.AnyAsync(x => x.Email == email);
        }

        public async Task<(bool Ok, string Error)> CreateAsync(string email, string password)
        {
            email = NormalizeEmail(email);

            if (!IsAllowedEmail(email))
                return (false, "Email invalid. Trebuie să fie @wizrom.ro");

            if (!IsPasswordComplex(password))
                return (false, "Parola nu respectă regula de complexitate (minim 12, mare/mic, cifră, simbol).");

            var exists = await _db.UserAccounts.AnyAsync(x => x.Email == email);
            if (exists)
                return (false, "Există deja un cont pentru acest email.");

            // Salt 16 bytes (VARBINARY(16))
            var salt = RandomNumberGenerator.GetBytes(16);

            // Hash 64 bytes (VARBINARY(64))
            var hash = HashPassword(password, salt);

            var now = DateTime.UtcNow;

            var acc = new UserAccount
            {
                Email = email,
                PasswordSalt = salt,
                PasswordHash = hash,
                CreatedUtc = now,
                UpdatedUtc = now,
                IsActive = true
            };

            _db.UserAccounts.Add(acc);
            await _db.SaveChangesAsync();

            return (true, "");
        }

        public async Task<(bool Ok, string Error, UserAccount? Account)> ValidatePasswordAsync(string email, string password)
        {
            email = NormalizeEmail(email);

            if (!IsAllowedEmail(email))
                return (false, "Email invalid. Trebuie să fie @wizrom.ro", null);

            // tracked (ca să putem salva UpdatedUtc ușor)
            var acc = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
            if (acc == null)
                return (false, "Nu există cont. Creează-l mai jos.", null);

            if (!acc.IsActive)
                return (false, "Cont dezactivat.", null);

            if (acc.PasswordSalt == null || acc.PasswordSalt.Length != 16 ||
                acc.PasswordHash == null || acc.PasswordHash.Length != 64)
                return (false, "Cont invalid (hash/salt lipsă sau dimensiuni greșite).", null);

            var computed = HashPassword(password, acc.PasswordSalt);

            if (!CryptographicOperations.FixedTimeEquals(computed, acc.PasswordHash))
                return (false, "Email sau parolă incorecte.", null);

            acc.UpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return (true, "", acc);
        }

	    /// <summary>
	    /// Resetează parola pentru un cont existent (folosit la "Am uitat parola").
	    /// </summary>
	    public async Task<(bool Ok, string Error)> SetPasswordByEmailAsync(string email, string newPassword)
	    {
	        email = NormalizeEmail(email);
	
	        if (!IsAllowedEmail(email))
	            return (false, "Email invalid. Trebuie să fie @wizrom.ro");
	
	        if (!IsPasswordComplex(newPassword))
	            return (false, "Parola nu respectă regula de complexitate (minim 12, mare/mic, cifră, simbol).");
	
	        var acc = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Email == email);
	        if (acc == null)
	            return (false, "Nu există cont. Creează-l mai jos.");
	
	        if (!acc.IsActive)
	            return (false, "Cont dezactivat.");
	
	        var salt = RandomNumberGenerator.GetBytes(16);
	        var hash = HashPassword(newPassword, salt);
	
	        acc.PasswordSalt = salt;
	        acc.PasswordHash = hash;
	        acc.UpdatedUtc = DateTime.UtcNow;
	
	        await _db.SaveChangesAsync();
	        return (true, "");
	    }

        // ============================
        // Crypto helpers
        // ============================
        private static byte[] HashPassword(string password, byte[] salt)
        {
            // iteratii: 100k ok pentru început
            const int iterations = 100_000;
            const int outBytes = 64; // IMPORTANT: 64 bytes = VARBINARY(64)

            return Rfc2898DeriveBytes.Pbkdf2(
                password: password ?? "",
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: outBytes
            );
        }

        public static bool IsPasswordComplex(string? password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            if (password.Length < 12) return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSymbol;
        }
    }
}
