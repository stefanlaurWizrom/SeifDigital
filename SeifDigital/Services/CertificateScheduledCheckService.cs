using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;

namespace SeifDigital.Services
{
    /// <summary>
    /// Scheduled service pentru verificarea automata a certificatelor si trimiterea alertelor
    /// Executa o data pe zi la ora setata in setari
    /// </summary>
    public class CertificateScheduledCheckService
    {
        private readonly ApplicationDbContext _db;
        private readonly CertificateCheckService _certCheckService;
        private readonly CertificateSettingsService _settingsService;
        private readonly SmtpEmailSender _emailSender;

        public CertificateScheduledCheckService(
            ApplicationDbContext db,
            CertificateCheckService certCheckService,
            CertificateSettingsService settingsService,
            SmtpEmailSender emailSender)
        {
            _db = db;
            _certCheckService = certCheckService;
            _settingsService = settingsService;
            _emailSender = emailSender;
        }

        /// <summary>
        /// Executa verificarea automata a tuturor certificatelor
        /// Se apeleaza de Hangfire la ora setata zilnic
        /// </summary>
        public async Task ExecuteAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CertificateScheduledCheck] Starting scheduled check...");

                // Get settings
                var verificationHour = await _settingsService.GetVerificationHourAsync();
                var timezone = await _settingsService.GetTimezoneAsync();
                var alertDaysThreshold = await _settingsService.GetAlertDaysThresholdAsync();
                var alertEmail = await _settingsService.GetAlertEmailAsync();

                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Settings - Hour: {verificationHour}, TZ: {timezone}, Threshold: {alertDaysThreshold}, Email: {alertEmail}");

                // Get all managed certificates
                var certificates = await _db.ManagedCertificates.ToListAsync();

                if (!certificates.Any())
                {
                    System.Diagnostics.Debug.WriteLine("[CertificateScheduledCheck] No certificates to check");
                    LogAuditEvent("CertificateCheck.Scheduled", "Success", "No certificates found");
                    return;
                }

                int checkedCount = 0;
                int alertedCount = 0;
                var alertsSent = new List<string>();

                // Loop through each certificate
                foreach (var cert in certificates)
                {
                    try
                    {
                        // Refresh certificate info
                        var checkResult = await _certCheckService.CheckCertificateAsync(
                            cert.Url,
                            cert.OriginServerIP,
                            cert.OriginServerPort);

                        // Update certificate data
                        cert.Status = checkResult.Status;
                        cert.CertificateExpiryDate = checkResult.CertificateExpiryDate;
                        cert.DaysUntilExpiry = checkResult.DaysUntilExpiry;
                        cert.CertificateSubject = checkResult.CertificateSubject;
                        cert.CertificateIssuer = checkResult.CertificateIssuer;
                        cert.ErrorMessage = checkResult.ErrorMessage;
                        cert.LastCheckDate = checkResult.LastCheckDate;
                        cert.OriginCertificateExpiryDate = checkResult.OriginCertificateExpiryDate;
                        cert.OriginCertificateSubject = checkResult.OriginCertificateSubject;
                        cert.OriginCertificateIssuer = checkResult.OriginCertificateIssuer;
                        cert.VerificationMethod = checkResult.VerificationMethod;
                        cert.IsCertificateMismatch = checkResult.IsCertificateMismatch;
                        cert.OriginDaysUntilExpiry = checkResult.OriginDaysUntilExpiry;

                        _db.ManagedCertificates.Update(cert);
                        checkedCount++;

                        // Check if alert should be sent
                        // Alert if: DaysUntilExpiry <= threshold (Expiring Soon or Expired)
                        if (cert.DaysUntilExpiry.HasValue && cert.DaysUntilExpiry <= alertDaysThreshold)
                        {
                            string alertType = cert.DaysUntilExpiry <= 0 ? "Expired" : "Expiring Soon";
                            string alertMessage = BuildAlertMessage(cert, alertType);

                            try
                            {
                                // Send email
                                _emailSender.Send(
                                    alertEmail,
                                    $"⚠️ Certificat SSL {alertType} - {cert.Url}",
                                    alertMessage);

                                // Log alert in CertificateAlertLog
                                _db.CertificateAlertLogs.Add(new CertificateAlertLog
                                {
                                    ManagedCertificateId = cert.Id,
                                    CertificateUrl = cert.Url,
                                    DaysUntilExpiry = cert.DaysUntilExpiry.Value,
                                    AlertType = alertType,
                                    EmailSentTo = alertEmail,
                                    AlertSentDateUtc = DateTime.UtcNow,
                                    EmailStatus = "Sent"
                                });

                                alertedCount++;
                                alertsSent.Add($"{cert.Url} ({cert.DaysUntilExpiry} zile)");

                                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Alert sent for: {cert.Url}");
                            }
                            catch (Exception emailEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Email error for {cert.Url}: {emailEx.Message}");

                                // Log failed alert
                                _db.CertificateAlertLogs.Add(new CertificateAlertLog
                                {
                                    ManagedCertificateId = cert.Id,
                                    CertificateUrl = cert.Url,
                                    DaysUntilExpiry = cert.DaysUntilExpiry.Value,
                                    AlertType = alertType,
                                    EmailSentTo = alertEmail,
                                    AlertSentDateUtc = DateTime.UtcNow,
                                    EmailStatus = "Failed",
                                    ErrorMessage = emailEx.Message
                                });
                            }
                        }
                    }
                    catch (Exception certEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Error checking certificate {cert.Url}: {certEx.Message}");
                        // Continue with next certificate
                    }
                }

                // Save all changes
                await _db.SaveChangesAsync();

                // Log audit event
                var auditDetails = new
                {
                    checked_count = checkedCount,
                    alerted_count = alertedCount,
                    alerts_sent = alertsSent
                };

                LogAuditEvent("CertificateCheck.Scheduled", "Success", System.Text.Json.JsonSerializer.Serialize(auditDetails));

                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Completed - Checked: {checkedCount}, Alerted: {alertedCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Fatal error: {ex.Message}");
                LogAuditEvent("CertificateCheck.Scheduled", "Fail", ex.Message);
            }
        }

        /// <summary>
        /// Construieste mesajul de alerta pentru email
        /// </summary>
        private string BuildAlertMessage(ManagedCertificate cert, string alertType)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("⚠️ ALERTA CERTIFICAT SSL/TLS");
            sb.AppendLine("================================\n");

            sb.AppendLine($"Site: {cert.Url}");
            sb.AppendLine($"Status: {alertType}");
            sb.AppendLine();

            if (cert.DaysUntilExpiry.HasValue)
            {
                if (cert.DaysUntilExpiry <= 0)
                {
                    sb.AppendLine($"🔴 EXPIRAT: Certificatul a expirat");
                }
                else
                {
                    sb.AppendLine($"🟠 Certificatul expira in {cert.DaysUntilExpiry} zile");
                }
            }

            if (cert.CertificateExpiryDate.HasValue)
            {
                sb.AppendLine($"Data expirare: {cert.CertificateExpiryDate:dd.MM.yyyy HH:mm:ss}");
            }

            sb.AppendLine();
            sb.AppendLine("Detalii certificat:");
            sb.AppendLine($"  Subject: {cert.CertificateSubject}");
            sb.AppendLine($"  Issuer: {cert.CertificateIssuer}");

            if (!string.IsNullOrEmpty(cert.VerificationMethod))
            {
                sb.AppendLine($"  Verificare: {cert.VerificationMethod}");
            }

            if (cert.IsCertificateMismatch)
            {
                sb.AppendLine($"  ⚠️ MISMATCH: Certificat CDN diferit de cel de origine");
            }

            sb.AppendLine();
            sb.AppendLine("================================");
            sb.AppendLine("Actiune recomandada: Renew certificatul urgent!");
            sb.AppendLine("================================\n");

            return sb.ToString();
        }

        /// <summary>
        /// Logeaza evenimentul in audit log
        /// </summary>
        private void LogAuditEvent(string eventType, string outcome, string details)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EventTimeUtc = DateTime.UtcNow,
                    EventType = eventType,
                    ActorUser = "SYSTEM",
                    ActorSid = null,
                    TargetType = "Certificate",
                    TargetId = null,
                    Outcome = outcome,
                    Reason = null,
                    ClientIp = null,
                    UserAgent = null,
                    CorrelationId = null,
                    DetailsJson = details
                });

                _db.SaveChangesAsync().Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CertificateScheduledCheck] Error logging audit: {ex.Message}");
            }
        }
    }
}
