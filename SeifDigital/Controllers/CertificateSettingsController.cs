using Microsoft.AspNetCore.Mvc;
using SeifDigital.Services;
using Hangfire;
using System.Collections.Generic;

namespace SeifDigital.Controllers
{
    public class CertificateSettingsController : Controller
    {
        private readonly CertificateSettingsService _settingsService;
        private readonly SmtpEmailSender _emailSender;
        private readonly AuditService _audit;

        public CertificateSettingsController(
            CertificateSettingsService settingsService,
            SmtpEmailSender emailSender,
            AuditService audit)
        {
            _settingsService = settingsService;
            _emailSender = emailSender;
            _audit = audit;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "1";
        }

        /// <summary>
        /// GET: Display certificate settings form
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                _audit.Log(HttpContext, "CertificateSettings.AccessDenied", "Fail", reason: "NotAdmin");
                return Forbid();
            }

            try
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                var timezones = CertificateSettingsService.GetAvailableTimezones();

                ViewBag.Timezones = timezones;
                ViewBag.CurrentTimezone = settings.Timezone;

                _audit.Log(HttpContext, "CertificateSettings.View", "Success");
                return View(settings);
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateSettings.View", "Fail", reason: ex.Message);
                ViewBag.Error = "Eroare la incarcarea setarilor";
                return View();
            }
        }

        /// <summary>
        /// POST: Save certificate settings
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(CertificateSettingsViewModel model)
        {
            if (!IsAdmin())
            {
                _audit.Log(HttpContext, "CertificateSettings.Save", "Fail", reason: "NotAdmin");
                return Forbid();
            }

            try
            {
                if (model == null)
                {
                    return BadRequest("Invalid settings data");
                }

                // Validate model
                if (model.VerificationHour < 0 || model.VerificationHour > 23)
                {
                    return BadRequest("Verification hour must be between 0 and 23");
                }

                if (model.AlertDaysThreshold < 1)
                {
                    return BadRequest("Alert days threshold must be at least 1");
                }

                if (string.IsNullOrWhiteSpace(model.AlertEmail) || !model.AlertEmail.Contains("@"))
                {
                    return BadRequest("Invalid email address");
                }

                // Save settings
                await _settingsService.SaveAllSettingsAsync(model);

                // ✅ UPDATE HANGFIRE JOB SCHEDULE IMMEDIATELY
                try
                {
                    string newCronExpression = $"{model.VerificationMinute} {model.VerificationHour} * * *";

                    RecurringJob.AddOrUpdate<CertificateScheduledCheckService>(
                        "certificate-check",
                        service => service.ExecuteAsync(),
                        newCronExpression,
                        System.TimeZoneInfo.FindSystemTimeZoneById(model.Timezone));

                    System.Diagnostics.Debug.WriteLine($"[SaveSettings] Hangfire job updated - Cron: {newCronExpression}, Timezone: {model.Timezone}");
                }
                catch (Exception hangfireEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SaveSettings] Warning: Failed to update Hangfire job: {hangfireEx.Message}");
                    // Continue anyway - settings are saved even if Hangfire update fails
                }

                _audit.Log(HttpContext, "CertificateSettings.Save", "Success",
                    details: new
                    {
                        hour = model.VerificationHour,
                        timezone = model.Timezone,
                        threshold = model.AlertDaysThreshold,
                        email = model.AlertEmail
                    });

                TempData["SuccessMessage"] = "Setarile au fost salvate cu succes!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateSettings.Save", "Fail", reason: ex.Message);
                TempData["ErrorMessage"] = $"Eroare: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// POST: Send test email
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestEmail()
        {
            if (!IsAdmin())
            {
                _audit.Log(HttpContext, "CertificateSettings.TestEmail", "Fail", reason: "NotAdmin");
                return Forbid();
            }

            try
            {
                var alertEmail = await _settingsService.GetAlertEmailAsync();

                var subject = "🧪 Test Email - Certificate Alert System";
                var body = @"
Acesta este un email de test din sistemul de alerta pentru certificatele SSL/TLS.

Daca primesti acest email, inseamna ca:
✅ Configuratia SMTP este corecta
✅ Email-ul pentru alerte este setat corect
✅ Sistemul poate trimite notificari

Teste programate pentru certificatele din lista se vor executa la ora setata.

---
Test Email - Certificate Alert System
";

                _emailSender.Send(alertEmail, subject, body);

                _audit.Log(HttpContext, "CertificateSettings.TestEmail", "Success",
                    details: new { email = alertEmail });

                TempData["SuccessMessage"] = $"Email test trimis cu succes la: {alertEmail}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateSettings.TestEmail", "Fail", reason: ex.Message);
                TempData["ErrorMessage"] = $"Eroare la trimiterea email-ului: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
