using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;
using System.Security.Cryptography;
using System.Text;

namespace SeifDigital.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserAccountService _accounts;
        private readonly SmtpEmailSender _email;
        private readonly AuditService _audit;

        public AccountController(UserAccountService accounts, SmtpEmailSender email, AuditService audit)
        {
            _accounts = accounts;
            _email = email;
            _audit = audit;
        }

        [HttpGet]
        public IActionResult Login(string? email = null)
        {
            ViewBag.Email = email ?? "";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string mode)
        {
            email = UserAccountService.NormalizeEmail(email);
            mode = (mode ?? "").Trim().ToLowerInvariant();

            // reguli email
            if (!UserAccountService.IsAllowedEmail(email))
            {
                ViewBag.Mesaj = "Email invalid. Trebuie să fie @wizrom.ro";
                ViewBag.Email = email;
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Mesaj = "Parola este obligatorie.";
                ViewBag.Email = email;
                return View();
            }

            // ============ CREATE ============
            if (mode == "create")
            {
                // UX clar: dacă există deja, nu încerca să-l "recreezi"
                if (await _accounts.EmailExistsAsync(email))
                {
                    ViewBag.Mesaj = "Există deja cont pentru acest email. Folosește butonul Login.";
                    ViewBag.Email = email;
                    return View();
                }

                var (ok, err) = await _accounts.CreateAsync(email, password);
                if (!ok)
                {
                    ViewBag.Mesaj = err;
                    ViewBag.Email = email;
                    return View();
                }

                _audit.Log(HttpContext, "Account.Create", "Success", details: new { email });

                // după creare -> continuăm cu login normal (trimite 2FA)
            }
            // ============ LOGIN ============
            else if (mode != "login")
            {
                // dacă vine ceva ciudat din UI, îl tratăm ca login
                mode = "login";
            }

            // 2) verifică parola / existența contului
            var (okLogin, errLogin, acc) = await _accounts.ValidatePasswordAsync(email, password);
            if (!okLogin || acc == null)
            {
                // UX dedicat când nu există cont
                if (errLogin.Contains("Nu există cont", StringComparison.OrdinalIgnoreCase))
                    ViewBag.Mesaj = "Nu există cont pentru acest email. Apasă „Creează cont”.";
                else
                    // mesaj generic pentru credențiale greșite
                    ViewBag.Mesaj = "Utilizator sau parola gresita";

                ViewBag.Email = email;

                _audit.Log(HttpContext, "Account.Login", "Fail",
                    reason: "InvalidCredentialsOrNoAccount",
                    details: new { email });

                return View();
            }

            // 3) generează OTP + trimite email
            var otp = GenerateOtp6();

            HttpContext.Session.SetString("LoginEmail", email);
            HttpContext.Session.SetString("OtpCode", otp);
            HttpContext.Session.SetString("OtpTimeUtc", DateTime.UtcNow.ToString("O"));

            // reset status
            HttpContext.Session.Remove("Status2FA");
            HttpContext.Session.Remove("OwnerKey");

            var subject = "Codul tău de acces WizVault";
            var body =
$@"Bună,

Codul tău de securitate este: {otp}

Codul este valabil 5 minute.

Dacă NU ai cerut acest cod, ignoră acest email.";

            try
            {
                _email.Send(email, subject, body);

                _audit.Log(HttpContext, "2FA.SendCode", "Success", details: new { email });

                return RedirectToAction(nameof(Verify2FA));
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "2FA.SendCode", "Fail",
                    reason: "SmtpError",
                    details: new { email, error = ex.Message });

                ViewBag.Mesaj = "Nu pot trimite emailul cu codul. Verifică SMTP. " + ex.Message;
                ViewBag.Email = email;
                return View();
            }
        }

        [HttpGet]
        public IActionResult Verify2FA()
        {
            ViewBag.Email = HttpContext.Session.GetString("LoginEmail") ?? "";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(string code)
        {
            var email = HttpContext.Session.GetString("LoginEmail") ?? "";
            var correct = HttpContext.Session.GetString("OtpCode");
            var timeStr = HttpContext.Session.GetString("OtpTimeUtc");

            if (string.IsNullOrWhiteSpace(correct) || string.IsNullOrWhiteSpace(timeStr) ||
                !DateTime.TryParse(timeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t0))
            {
                ViewBag.Mesaj = "Nu există un cod activ. Te rog re-login.";
                ViewBag.Email = email;
                return View();
            }

            if (DateTime.UtcNow - t0 > TimeSpan.FromMinutes(5))
            {
                ViewBag.Mesaj = "Cod expirat. Te rog re-login.";
                ViewBag.Email = email;
                return View();
            }

            var entered = (code ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(entered) && entered == correct)
            {
                HttpContext.Session.SetString("Status2FA", "Validat");
                HttpContext.Session.SetString("OwnerKey", email); // cheia vault = email
                var acc = await _accounts.FindByEmailAsync(email);
                HttpContext.Session.SetString("IsAdmin", (acc?.IsAdmin == true) ? "1" : "0");


                HttpContext.Session.Remove("OtpCode");
                HttpContext.Session.Remove("OtpTimeUtc");

                _audit.Log(HttpContext, "2FA.Verify", "Success", details: new { email });

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Mesaj = "Cod incorect.";
            ViewBag.Email = email;

            _audit.Log(HttpContext, "2FA.Verify", "Fail", reason: "WrongCode", details: new { email });

            return View();
        }

	    // ─────────────────────────────────────────────────────────────────────────────
	    // Forgot password (reset) flow
	    // ─────────────────────────────────────────────────────────────────────────────
	    [HttpGet]
	    public IActionResult ForgotPassword()
	    {
	        ViewBag.Email = "";
	        ViewBag.Mesaj = "";
	        return View();
	    }

	    [HttpPost]
	    [ValidateAntiForgeryToken]
	    public async Task<IActionResult> ForgotPassword(string email)
	    {
	        email = (email ?? "").Trim().ToLowerInvariant();
	        if (string.IsNullOrWhiteSpace(email))
	        {
	            ViewBag.Mesaj = "Introdu emailul.";
	            return View();
	        }

	        var acc = await _accounts.FindByEmailAsync(email);
	        if (acc == null)
	        {
	            ViewBag.Mesaj = "Nu există cont pentru acest email.";
	            ViewBag.Email = email;
	            return View();
	        }

	        // Generează cod reset + persistă în sesiune
	        var code = GenerateOtp6();
	        HttpContext.Session.SetString("ResetEmail", email);
	        HttpContext.Session.SetString("ResetCode", code);
	        HttpContext.Session.Remove("ResetVerified");

	        // Trimite email (același mecanism ca 2FA)
	        try
	        {
	            var subject = "WizVault - Cod resetare parolă";
	            var body = $"Codul tău pentru resetarea parolei este: {code}\n\nDacă nu ai cerut resetarea, ignoră acest email.";
	            _email.Send(email, subject, body);
	        }
	        catch
	        {
	            ViewBag.Mesaj = "Nu s-a putut trimite emailul. Încearcă din nou.";
	            ViewBag.Email = email;
	            return View();
	        }

	        return RedirectToAction(nameof(VerifyResetCode));
	    }

	    [HttpGet]
	    public IActionResult VerifyResetCode()
	    {
	        var email = HttpContext.Session.GetString("ResetEmail") ?? "";
	        if (string.IsNullOrWhiteSpace(email))	// flow invalid
	            return RedirectToAction(nameof(Login));

	        ViewBag.Email = email;
	        ViewBag.Mesaj = "";
	        return View();
	    }

	    [HttpPost]
	    [ValidateAntiForgeryToken]
	    public IActionResult VerifyResetCode(string code)
	    {
	        var expected = HttpContext.Session.GetString("ResetCode") ?? "";
	        var email = HttpContext.Session.GetString("ResetEmail") ?? "";
	
	        if (string.IsNullOrWhiteSpace(email))
	            return RedirectToAction(nameof(Login));

	        if (!string.IsNullOrWhiteSpace(code) && code.Trim() == expected)
	        {
	            HttpContext.Session.SetString("ResetVerified", "1");
	            return RedirectToAction(nameof(ResetPassword));
	        }

	        ViewBag.Mesaj = "Cod incorect.";
	        ViewBag.Email = email;
	        return View();
	    }

	    [HttpGet]
	    public IActionResult ResetPassword()
	    {
	        var email = HttpContext.Session.GetString("ResetEmail") ?? "";
	        var ok = HttpContext.Session.GetString("ResetVerified") == "1";
	
	        if (!ok || string.IsNullOrWhiteSpace(email))
	            return RedirectToAction(nameof(Login));

	        ViewBag.Email = email;
	        ViewBag.Mesaj = "";
	        return View();
	    }

	    [HttpPost]
	    [ValidateAntiForgeryToken]
	    public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
	    {
	        var email = HttpContext.Session.GetString("ResetEmail") ?? "";
	        var ok = HttpContext.Session.GetString("ResetVerified") == "1";
	
	        if (!ok || string.IsNullOrWhiteSpace(email))
	            return RedirectToAction(nameof(Login));

	        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12)
	        {
	            ViewBag.Mesaj = "Parola trebuie să aibă minim 12 caractere.";
	            ViewBag.Email = email;
	            return View();
	        }
	        if (newPassword != (confirmPassword ?? ""))
	        {
	            ViewBag.Mesaj = "Parolele nu coincid.";
	            ViewBag.Email = email;
	            return View();
	        }

	        var (okSet, err) = await _accounts.SetPasswordByEmailAsync(email, newPassword);
	        if (!okSet)
	        {
	            ViewBag.Mesaj = err;
	            ViewBag.Email = email;
	            return View();
	        }

	        // curăță flow reset
	        HttpContext.Session.Remove("ResetEmail");
	        HttpContext.Session.Remove("ResetCode");
	        HttpContext.Session.Remove("ResetVerified");

	        TempData["ResetOk"] = "Parola a fost schimbată. Te poți autentifica.";
	        return RedirectToAction(nameof(Login));
	    }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var email = HttpContext.Session.GetString("LoginEmail") ?? "";
            _audit.Log(HttpContext, "Account.Logout", "Success", details: new { email });

            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
            return RedirectToAction(nameof(Login));
        }

        // OTP 6 digits (crypto-safe)
        private static string GenerateOtp6()
        {
            // 0..999999
            var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return n.ToString("D6");
        }
    }
}
