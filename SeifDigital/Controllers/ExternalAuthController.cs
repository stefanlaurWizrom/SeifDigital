using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;
using System.Globalization;

namespace SeifDigital.Controllers
{
    public class ExternalAuthController : Controller
    {
        private readonly SmtpEmailSender _email;
        private readonly AuditService _audit;

        public ExternalAuthController(SmtpEmailSender email, AuditService audit)
        {
            _email = email;
            _audit = audit;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendCode(string email)
        {
            // normalizare
            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email) || !email.EndsWith("@wizrom.ro", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Mesaj = "Email invalid. Trebuie să fie @wizrom.ro";
                _audit.Log(HttpContext, "Ext2FA.SendCode", "Fail", reason: "InvalidEmail", details: new { email });
                return View("Login");
            }

            const int COOLDOWN_SECONDS = 60;

            string? nextStr = HttpContext.Session.GetString("ExtOtpNextSendUtc");
            if (!string.IsNullOrWhiteSpace(nextStr) &&
                DateTime.TryParse(nextStr, null, DateTimeStyles.RoundtripKind, out var nextUtc) &&
                DateTime.UtcNow < nextUtc)
            {
                var secLeft = (int)Math.Ceiling((nextUtc - DateTime.UtcNow).TotalSeconds);
                ViewBag.Mesaj = $"Ai cerut deja un cod. Încearcă din nou peste ~{secLeft} secunde.";
                _audit.Log(HttpContext, "Ext2FA.SendCode", "Fail", reason: "Cooldown", details: new { secLeft, email });
                return View("Login");
            }

            string codGenerat = new Random().Next(100000, 999999).ToString();

            // salvăm în sesiune flow-ul extern
            HttpContext.Session.SetString("ExtEmail", email);
            HttpContext.Session.SetString("ExtCodSecret", codGenerat);
            HttpContext.Session.SetString("ExtCodSecretTime", DateTime.UtcNow.ToString("O"));
            HttpContext.Session.SetString("ExtOtpNextSendUtc", DateTime.UtcNow.AddSeconds(COOLDOWN_SECONDS).ToString("O"));

            // reset acces (trebuie să verifice codul)
            HttpContext.Session.Remove("Status2FA");
            HttpContext.Session.Remove("OwnerKey");

            string subject = "Codul tău de acces Seif Digital";
            string body =
$@"Bună,

Ai cerut acces în aplicația Seif Digital.

Codul tău de securitate este: {codGenerat}

Codul este valabil 5 minute.

Dacă NU ai cerut acest cod, ignoră acest email.";

            try
            {
                _email.Send(email, subject, body);
                _audit.Log(HttpContext, "Ext2FA.SendCode", "Success", details: new { to = email });
                ViewBag.Info = "Cod trimis. Verifică emailul și introdu codul.";
                ViewBag.Email = email;
                return View("Login");
            }
            catch (Exception ex)
            {
                ViewBag.Mesaj = "Eroare la trimiterea emailului: " + ex.Message;
                _audit.Log(HttpContext, "Ext2FA.SendCode", "Fail", reason: "SmtpError", details: new { error = ex.Message, email });
                return View("Login");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCode(string codIntrodus)
        {
            const string KEY_CODE = "ExtCodSecret";
            const string KEY_TIME = "ExtCodSecretTime";

            string? email = HttpContext.Session.GetString("ExtEmail");
            string? codCorect = HttpContext.Session.GetString(KEY_CODE);
            string? timeStr = HttpContext.Session.GetString(KEY_TIME);

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(codCorect) ||
                string.IsNullOrWhiteSpace(timeStr) ||
                !DateTime.TryParse(timeStr, null, DateTimeStyles.RoundtripKind, out var t0))
            {
                ViewBag.Mesaj = "Nu există un cod activ. Cere un cod nou.";
                _audit.Log(HttpContext, "Ext2FA.Verify", "Fail", reason: "NoActiveCode");
                return View("Login");
            }

            // normalizare (siguranță)
            email = email.Trim().ToLowerInvariant();

            if (DateTime.UtcNow - t0 > TimeSpan.FromMinutes(5))
            {
                ViewBag.Mesaj = "Cod expirat. Cere un cod nou.";
                _audit.Log(HttpContext, "Ext2FA.Verify", "Fail", reason: "ExpiredCode", details: new { email });
                return View("Login");
            }

            if (!string.IsNullOrWhiteSpace(codIntrodus) && codIntrodus == codCorect)
            {
                HttpContext.Session.SetString("Status2FA", "Validat");

                // IMPORTANT: OwnerKey=email (același vault ca Windows)
                HttpContext.Session.SetString("OwnerKey", email);

                // ✅ PUNCTUL 4: curățăm cheile Ext* după succes (nu mai păstrăm cod/timeout)
                HttpContext.Session.Remove("ExtCodSecret");
                HttpContext.Session.Remove("ExtCodSecretTime");
                HttpContext.Session.Remove("ExtOtpNextSendUtc");
                // ExtEmail îl poți păstra (pentru UX) sau îl poți șterge:
                // HttpContext.Session.Remove("ExtEmail");

                _audit.Log(HttpContext, "Ext2FA.Verify", "Success", details: new { ownerKey = email });

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Mesaj = "Cod incorect.";
            _audit.Log(HttpContext, "Ext2FA.Verify", "Fail", reason: "WrongCode", details: new { email });
            return View("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            _audit.Log(HttpContext, "ExtAuth.Logout", "Success");
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            return RedirectToAction("Login");
        }
    }
}
