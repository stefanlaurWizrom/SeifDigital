using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;
using SeifDigital.Services;
using System.Security.Cryptography;
using System.Text;

namespace SeifDigital.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly SmtpEmailSender _emailSender;

        public AdminController(ApplicationDbContext context, AuditService audit, SmtpEmailSender emailSender)
        {
            _context = context;
            _audit = audit;
            _emailSender = emailSender;
        }

        // Verifică dacă userul curent este admin
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "1";
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Verifică permisiuni
            if (!IsAdmin())
            {
                _audit.Log(HttpContext, "Admin.AccessDenied", "Fail", reason: "NotAdmin");
                return Forbid();
            }

            // Statistici admin
            var totalUsers = await _context.UserAccounts.CountAsync();
            var totalAdmins = await _context.UserAccounts.Where(u => u.IsAdmin).CountAsync();
            var totalAuditLogs = await _context.AuditLogs.CountAsync();
            var totalPasswords = await _context.InformatiiSensibile.CountAsync();
            var totalNotes = await _context.UserNotes.CountAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalAuditLogs = totalAuditLogs;
            ViewBag.TotalPasswords = totalPasswords;
            ViewBag.TotalNotes = totalNotes;

            _audit.Log(HttpContext, "Admin.Index", "Success");

            return View();
        }

        // API: Lista utilizatori
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            if (!IsAdmin())
                return Forbid();

            var users = await _context.UserAccounts
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.IsAdmin,
                    u.IsActive,
                    u.CreatedUtc,
                    u.UpdatedUtc
                })
                .ToListAsync();

            return Json(users);
        }

        // Toggle admin status
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleAdmin(long userId)
        {
            if (!IsAdmin())
                return Forbid();

            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null)
                return NotFound();

            var oldStatus = user.IsAdmin;
            user.IsAdmin = !user.IsAdmin;
            user.UpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _audit.Log(HttpContext, "Admin.ToggleAdmin", "Success",
                details: new 
                { 
                    userId, 
                    email = user.Email,
                    oldStatus = oldStatus,
                    newStatus = user.IsAdmin,
                    action = oldStatus ? "Demis din Admin" : "Promovat la Admin"
                });

            return Ok(new { success = true, isAdmin = user.IsAdmin });
        }

        // Toggle active status
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleActive(long userId)
        {
            if (!IsAdmin())
                return Forbid();

            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null)
                return NotFound();

            var oldStatus = user.IsActive;
            user.IsActive = !user.IsActive;
            user.UpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _audit.Log(HttpContext, "Admin.ToggleActive", "Success",
                details: new 
                { 
                    userId, 
                    email = user.Email,
                    oldStatus = oldStatus,
                    newStatus = user.IsActive,
                    action = oldStatus ? "Dezactivat" : "Reactivat"
                });

            return Ok(new { success = true, isActive = user.IsActive });
        }

        // Detalii utilizator
        [HttpGet]
        public async Task<IActionResult> UserDetails(long userId)
        {
            if (!IsAdmin())
                return Forbid();

            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null)
                return NotFound();

            var passwordCount = await _context.InformatiiSensibile
                .Where(p => p.OwnerKey == user.Email)
                .CountAsync();

            var noteCount = await _context.UserNotes
                .Where(n => n.OwnerUser == user.Email)
                .CountAsync();

            return Ok(new
            {
                user.Id,
                user.Email,
                user.IsAdmin,
                user.IsActive,
                user.CreatedUtc,
                user.UpdatedUtc,
                passwordCount,
                noteCount
            });
        }

        // Reset password - Generează parolă temporară
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ResetPassword(long userId)
        {
            if (!IsAdmin())
                return Forbid();

            var user = await _context.UserAccounts.FindAsync(userId);
            if (user == null)
                return NotFound();

            // Generează parolă temporară cu 16 caractere (complexă)
            var tempPassword = GenerateTemporaryPassword();

            // Hash noua parolă
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = HashPassword(tempPassword, salt);

            user.PasswordSalt = salt;
            user.PasswordHash = hash;
            user.UpdatedUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _audit.Log(HttpContext, "Admin.ResetPassword", "Success",
                details: new 
                { 
                    userId, 
                    email = user.Email,
                    action = "Parolă resetată"
                });

            return Ok(new 
            { 
                success = true, 
                message = "Parola resetată cu succes",
                temporaryPassword = tempPassword,
                email = user.Email
            });
        }

        // Generează o parolă temporară (complex, 16 caractere)
        private string GenerateTemporaryPassword()
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*";

            var password = new StringBuilder();
            var random = new Random();

            // Asigură cel puțin 1 din fiecare tip
            password.Append(uppercase[random.Next(uppercase.Length)]);
            password.Append(lowercase[random.Next(lowercase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(symbols[random.Next(symbols.Length)]);

            // Completează până la 16 caractere
            var allChars = uppercase + lowercase + digits + symbols;
            while (password.Length < 16)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Amestecă caracterele
            var chars = password.ToString().ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int randomIndex = random.Next(i + 1);
                var temp = chars[i];
                chars[i] = chars[randomIndex];
                chars[randomIndex] = temp;
            }

            return new string(chars);
        }

        // Hash password - Refolosit din UserAccountService
        private byte[] HashPassword(string password, byte[] salt)
        {
            const int iterations = 100_000;
            const int outBytes = 64;

            return Rfc2898DeriveBytes.Pbkdf2(
                password: password ?? "",
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: outBytes
            );
        }

        // Trimite parola temporară prin email
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SendPasswordResetEmail([FromBody] SendPasswordResetEmailRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            if (request == null)
                return BadRequest(new { success = false, message = "Request invalid" });

            var user = await _context.UserAccounts.FindAsync(request.UserId);
            if (user == null)
                return NotFound();

            // Validare email
            if (string.IsNullOrWhiteSpace(request.RecipientEmail))
                return BadRequest(new { success = false, message = "Email destinație este obligatoriu" });

            try
            {
                var subject = "🔐 WizVault - Resetare Parolă Temporară";
                var body = $@"
Bună,

Parola ta de acces a fost resetată de administratorul WizVault.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📧 EMAIL: {user.Email}
🔑 PAROLĂ TEMPORARĂ: {request.TemporaryPassword}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

⚠️  IMPORTANTE:
1. Accesează WizVault și autentifică-te cu parola de mai sus
2. La prima autentificare, vei fi solicitat să schimbi parola
3. Schimbă parola cu una personală complexă
4. NU împărtășești această parolă cu nimeni

Dacă NU ai cerut resetarea parolei, contactează imediat administratorul.

Mulțumim,
Echipa WizVault
";

                _emailSender.Send(request.RecipientEmail, subject, body);

                _audit.Log(HttpContext, "Admin.SendPasswordResetEmail", "Success",
                    details: new 
                    { 
                        userId = request.UserId, 
                        userEmail = user.Email, 
                        recipientEmail = request.RecipientEmail,
                        action = "Email trimis cu parolă temporară"
                    });

                return Ok(new 
                { 
                    success = true, 
                    message = $"Parola temporară a fost trimisă cu succes la {request.RecipientEmail}" 
                });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "Admin.SendPasswordResetEmail", "Fail",
                    reason: "SmtpError",
                    details: new 
                    { 
                        userId = request.UserId, 
                        userEmail = user.Email,
                        recipientEmail = request.RecipientEmail, 
                        error = ex.Message,
                        action = "Eroare la trimitere email"
                    });

                return BadRequest(new 
                { 
                    success = false, 
                    message = $"Eroare la trimiterea emailului: {ex.Message}" 
                });
            }
        }
    }
}

// Model pentru request-ul de trimitere email
namespace SeifDigital.Controllers
{
    public class SendPasswordResetEmailRequest
    {
        public long UserId { get; set; }
        public string TemporaryPassword { get; set; } = "";
        public string RecipientEmail { get; set; } = "";
    }
}
