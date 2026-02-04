using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SeifDigital.Services;
using SeifDigital.Utils;
using System;
using System.Globalization;

namespace SeifDigital.Controllers
{
    public class AuthController : Controller
    {
        private readonly SmtpEmailSender _email;
        private readonly AuditService _audit;
        private readonly UserProfileService _profiles;


        public AuthController(SmtpEmailSender email, AuditService audit, UserProfileService profiles)
        {
            _email = email;
            _audit = audit;
            _profiles = profiles;
        }


        // =========================
        // B9: PAGINA 2FA (GET) - NU trimite cod, doar afișează pagina + status cooldown
        // =========================
        [HttpGet]
        public IActionResult Verificare2FA()
        {
            // 1) verificăm Windows Authentication
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                ViewBag.Mesaj = "Windows Authentication NU funcționează. Userul nu este autentificat.";
                return View();
            }

            // 2) user de domeniu (debug)
            string domainUser = User.Identity?.Name ?? "";
            ViewBag.DebugUser = domainUser;

            // 3) info cooldown (dacă există)
            string? nextStr = HttpContext.Session.GetString("OtpNextSendUtc");
            if (!string.IsNullOrWhiteSpace(nextStr) &&
                DateTime.TryParse(nextStr, null, DateTimeStyles.RoundtripKind, out var nextUtc) &&
                DateTime.UtcNow < nextUtc)
            {
                var secLeft = (int)Math.Ceiling((nextUtc - DateTime.UtcNow).TotalSeconds);
                ViewBag.Info = $"Poți cere un cod nou peste ~{secLeft} secunde.";
            }

            return View();
        }

        // =========================
        // B9: TRIMITE COD (POST) - aici se generează și se trimite emailul
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrimiteCod()

        {
            // 1) verificăm Windows Authentication
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                _audit.Log(HttpContext, "2FA.SendCode", "Fail", reason: "WindowsAuth_NotAuthenticated");
                return RedirectToAction("Verificare2FA");
            }

            // 2) user de domeniu
            string domainUser = User.Identity?.Name ?? "";
            ViewBag.DebugUser = domainUser;

            if (string.IsNullOrWhiteSpace(domainUser))
            {
                ViewBag.Mesaj = "Nu pot determina userul de domeniu.";
                _audit.Log(HttpContext, "2FA.SendCode", "Fail", reason: "EmptyDomainUser");
                return View("Verificare2FA");
            }

            // 3) cooldown anti-spam (schimbă aici dacă vrei alt interval)
            const int COOLDOWN_SECONDS = 60;

            string? nextStr = HttpContext.Session.GetString("OtpNextSendUtc");
            if (!string.IsNullOrWhiteSpace(nextStr) &&
                DateTime.TryParse(nextStr, null, DateTimeStyles.RoundtripKind, out var nextUtc) &&
                DateTime.UtcNow < nextUtc)
            {
                var secLeft = (int)Math.Ceiling((nextUtc - DateTime.UtcNow).TotalSeconds);
                ViewBag.Mesaj = $"Ai cerut deja un cod. Încearcă din nou peste ~{secLeft} secunde.";

                _audit.Log(HttpContext, "2FA.SendCode", "Fail",
                    reason: "Cooldown",
                    details: new { secLeft });

                return View("Verificare2FA");
            }

            // 4) email din AD
            string? toEmail = null;

            // 1) încercăm AD
            try
            {
                toEmail = AdUserHelper.GetEmailFromDomainUser(domainUser);
                if (!string.IsNullOrWhiteSpace(toEmail))
                {
                    // salvăm în cache (sursă AD)
                    await _profiles.UpsertEmailAsync(domainUser, toEmail, "AD");
                }
            }
            catch (Exception ex)
            {
                // AD/DC indisponibil sau altă eroare
                _audit.Log(HttpContext, "2FA.SendCode", "Fail", reason: "AD_Lookup_Error",
                    details: new { error = ex.Message });
            }

            // 2) dacă AD nu a dat email, încercăm cache
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                toEmail = await _profiles.GetCachedEmailAsync(domainUser);

                if (!string.IsNullOrWhiteSpace(toEmail))
                {
                    // marcăm folosirea cache-ului (nu schimbăm LastVerifiedUtc)
                    await _profiles.UpsertEmailAsync(domainUser, toEmail, "CACHE");
                }
            }

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                ViewBag.Mesaj = $"Nu pot găsi emailul pentru userul: {domainUser}. (AD indisponibil și nu există email cache-uit.)";
                _audit.Log(HttpContext, "2FA.SendCode", "Fail", reason: "AdEmail_NotFound_And_NoCache");
                return View("Verificare2FA");
            }

            ViewBag.EmailTrimis = toEmail;


           

            // 5) generăm cod
            string codGenerat = new Random().Next(100000, 999999).ToString();

            // 6) salvăm cod + timpul în sesiune
            HttpContext.Session.SetString("CodSecret", codGenerat);
            HttpContext.Session.SetString("CodSecretTime", DateTime.UtcNow.ToString("O"));

            // IMPORTANT: când trimiți cod nou, 2FA devine nevalidat
            HttpContext.Session.Remove("Status2FA");

            // resetăm încercările când trimitem un cod nou
            HttpContext.Session.Remove("OtpFailCount");
            HttpContext.Session.Remove("OtpLockUntilUtc");

            // setăm când ai voie să ceri iar cod (cooldown)
            HttpContext.Session.SetString("OtpNextSendUtc", DateTime.UtcNow.AddSeconds(COOLDOWN_SECONDS).ToString("O"));

            // 7) trimitem email
            string subject = "Codul tău de acces Seif Digital";
            string body =
$@"Bună,

Ai cerut acces în aplicația Seif Digital.

Codul tău de securitate este: {codGenerat}

Codul este valabil 5 minute.

Dacă NU ai cerut acest cod, ignoră acest email.";

            try
            {
                _email.Send(toEmail, subject, body);

                _audit.Log(HttpContext, "2FA.SendCode", "Success",
                    details: new { to = toEmail });

                ViewBag.Info = "Cod trimis. Verifică emailul și introdu codul mai jos.";
                return View("Verificare2FA");
            }
            catch (Exception ex)
            {
                ViewBag.Mesaj = "Eroare la trimiterea emailului: " + ex.Message;

                _audit.Log(HttpContext, "2FA.SendCode", "Fail",
                    reason: "SmtpError",
                    details: new { error = ex.Message });

                return View("Verificare2FA");
            }
        }

        // =========================
        // PASUL 2: VERIFICARE COD
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerificaCod(string codIntrodus)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToAction("Verificare2FA");

            // Chei de sesiune
            const string KEY_CODE = "CodSecret";
            const string KEY_TIME = "CodSecretTime";
            const string KEY_FAILS = "OtpFailCount";
            const string KEY_LOCK_UNTIL = "OtpLockUntilUtc";

            // 1) blocare?
            string? lockStr = HttpContext.Session.GetString(KEY_LOCK_UNTIL);
            if (!string.IsNullOrWhiteSpace(lockStr) &&
                DateTime.TryParse(lockStr, null, DateTimeStyles.RoundtripKind, out var lockUntil))
            {
                if (DateTime.UtcNow < lockUntil)
                {
                    var minutesLeft = Math.Ceiling((lockUntil - DateTime.UtcNow).TotalMinutes);
                    ViewBag.Mesaj = $"Prea multe încercări greșite. Încearcă din nou peste ~{minutesLeft} minute.";

                    _audit.Log(HttpContext, "2FA.Verify", "Fail",
                        reason: "Locked",
                        details: new { minutesLeft });

                    return View("Verificare2FA");
                }
                else
                {
                    HttpContext.Session.Remove(KEY_LOCK_UNTIL);
                    HttpContext.Session.Remove(KEY_FAILS);
                }
            }

            // 2) cod activ + timp
            string? codCorect = HttpContext.Session.GetString(KEY_CODE);
            string? timeStr = HttpContext.Session.GetString(KEY_TIME);

            if (string.IsNullOrWhiteSpace(codCorect) || string.IsNullOrWhiteSpace(timeStr) ||
                !DateTime.TryParse(timeStr, null, DateTimeStyles.RoundtripKind, out var t0))
            {
                ViewBag.Mesaj = "Nu există un cod activ. Apasă «Trimite cod pe email» ca să primești un cod nou.";

                _audit.Log(HttpContext, "2FA.Verify", "Fail",
                    reason: "NoActiveCode");

                return View("Verificare2FA");
            }

            // 3) expirare 5 minute
            if (DateTime.UtcNow - t0 > TimeSpan.FromMinutes(5))
            {
                ViewBag.Mesaj = "Cod expirat. Apasă «Trimite cod pe email» ca să primești un cod nou.";

                _audit.Log(HttpContext, "2FA.Verify", "Fail",
                    reason: "ExpiredCode");

                return View("Verificare2FA");
            }

            // 4) verificare cod
            if (!string.IsNullOrWhiteSpace(codIntrodus) && codIntrodus == codCorect)
            {
                HttpContext.Session.SetString("Status2FA", "Validat");
                HttpContext.Session.Remove(KEY_FAILS);
                HttpContext.Session.Remove(KEY_LOCK_UNTIL);

                _audit.Log(HttpContext, "2FA.Verify", "Success");

                return RedirectToAction("Index", "Home");
            }

            // 5) cod greșit -> incrementăm
            int fails = 0;
            var failsStr = HttpContext.Session.GetString(KEY_FAILS);
            if (!string.IsNullOrWhiteSpace(failsStr))
                int.TryParse(failsStr, out fails);

            fails++;
            HttpContext.Session.SetString(KEY_FAILS, fails.ToString());

            // 6) la 5 -> blocare 10 min
            if (fails >= 5)
            {
                var until = DateTime.UtcNow.AddMinutes(10);
                HttpContext.Session.SetString(KEY_LOCK_UNTIL, until.ToString("O"));

                ViewBag.Mesaj = "Ai introdus cod greșit de prea multe ori. Ai fost blocat 10 minute.";

                _audit.Log(HttpContext, "2FA.Verify", "Fail",
                    reason: "TooManyAttempts",
                    details: new { fails, lockMinutes = 10 });

                return View("Verificare2FA");
            }

            // 7) mesaj normal
            int ramase = 5 - fails;
            ViewBag.Mesaj = $"Cod incorect! Mai ai {ramase} încercări până la blocare.";

            _audit.Log(HttpContext, "2FA.Verify", "Fail",
                reason: "WrongCode",
                details: new { fails, ramase });

            return View("Verificare2FA");
        }

        // =========================
        // PASUL 3: LOGOUT (reset 2FA)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            _audit.Log(HttpContext, "Auth.Logout", "Success");
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            return RedirectToAction("Verificare2FA");
        }
    }
}
