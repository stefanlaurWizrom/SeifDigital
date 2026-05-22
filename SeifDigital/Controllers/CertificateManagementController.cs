using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeifDigital.Data;
using SeifDigital.Models;
using SeifDigital.Services;

namespace SeifDigital.Controllers
{
    public class CertificateManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly CertificateCheckService _certificateCheckService;

        public CertificateManagementController(
            ApplicationDbContext context,
            AuditService audit,
            CertificateCheckService certificateCheckService)
        {
            _context = context;
            _audit = audit;
            _certificateCheckService = certificateCheckService;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("IsAdmin") == "1";
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAdmin())
            {
                _audit.Log(HttpContext, "CertificateManagement.AccessDenied", "Fail", reason: "NotAdmin");
                return Forbid();
            }

            try
            {
                // Query certificates directly without filters first
                var allCertificates = await _context.ManagedCertificates.ToListAsync();
                System.Diagnostics.Debug.WriteLine($"[CertificateManagement.Index] Total records in DB: {allCertificates.Count}");

                foreach (var cert in allCertificates)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Id: {cert.Id}, Url: '{cert.Url}', Status: '{cert.Status}'");
                }

                // Filter to exclude empty URLs
                var certificates = allCertificates
                    .Where(c => !string.IsNullOrWhiteSpace(c?.Url))
                    .OrderBy(c => c.Url)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[CertificateManagement.Index] Valid records after filter: {certificates.Count}");

                _audit.Log(HttpContext, "CertificateManagement.Index", "Success");
                return View(certificates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CertificateManagement.Index] Error: {ex.Message} - {ex.StackTrace}");
                _audit.Log(HttpContext, "CertificateManagement.Index", "Fail", reason: ex.Message);
                return View(new List<ManagedCertificate>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCertificate(string url, string? originIP = null, int? originPort = null)
        {
            if (!IsAdmin())
                return Forbid();

            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL-ul este obligatoriu");

            try
            {
                // Validare URL format
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Fail", 
                        targetId: url, reason: "InvalidUrl");
                    return BadRequest("Format URL invalid");
                }

                // Verifică dacă URL-ul deja există
                var existing = await _context.ManagedCertificates
                    .FirstOrDefaultAsync(c => c.Url == url);

                if (existing != null)
                {
                    _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Fail",
                        targetId: url, reason: "DuplicateUrl");
                    return BadRequest("URL-ul deja există în listă");
                }

                // Validare IP (opțional)
                int port = originPort ?? 443;
                if (!string.IsNullOrWhiteSpace(originIP))
                {
                    if (!System.Net.IPAddress.TryParse(originIP, out _))
                    {
                        _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Fail",
                            targetId: url, reason: "InvalidOriginIP");
                        return BadRequest("Format adresă IP invalid");
                    }

                    if (port < 1 || port > 65535)
                    {
                        _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Fail",
                            targetId: url, reason: "InvalidPort");
                        return BadRequest("Port invalid (1-65535)");
                    }
                }

                // Verifică certificatul(ele)
                var checkResult = await _certificateCheckService.CheckCertificateAsync(url, originIP, port);

                var certificate = new ManagedCertificate
                {
                    Url = url,
                    Status = checkResult.Status,
                    CertificateExpiryDate = checkResult.CertificateExpiryDate,
                    DaysUntilExpiry = checkResult.DaysUntilExpiry,
                    CertificateSubject = checkResult.CertificateSubject,
                    CertificateIssuer = checkResult.CertificateIssuer,
                    ErrorMessage = checkResult.ErrorMessage,
                    LastCheckDate = checkResult.LastCheckDate,
                    CreatedDate = DateTime.UtcNow,
                    OriginServerIP = originIP,
                    OriginServerPort = port,
                    OriginCertificateExpiryDate = checkResult.OriginCertificateExpiryDate,
                    OriginCertificateSubject = checkResult.OriginCertificateSubject,
                    OriginCertificateIssuer = checkResult.OriginCertificateIssuer,
                    VerificationMethod = checkResult.VerificationMethod,
                    IsCertificateMismatch = checkResult.IsCertificateMismatch,
                    OriginDaysUntilExpiry = checkResult.OriginDaysUntilExpiry
                };

                _context.ManagedCertificates.Add(certificate);
                await _context.SaveChangesAsync();

                _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Success",
                    targetId: url, targetType: "Certificate");

                return Ok(new { success = true, message = "Certificatul a fost adăugat cu succes" });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateManagement.AddCertificate", "Fail",
                    targetId: url, reason: $"Exception: {ex.Message}");
                return BadRequest($"Eroare: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCertificate(int id)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var certificate = await _context.ManagedCertificates.FindAsync(id);
                if (certificate == null)
                    return NotFound();

                _context.ManagedCertificates.Remove(certificate);
                await _context.SaveChangesAsync();

                _audit.Log(HttpContext, "CertificateManagement.RemoveCertificate", "Success",
                    targetId: id.ToString(), targetType: "Certificate");

                return Ok(new { success = true, message = "Certificatul a fost șters cu succes" });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateManagement.RemoveCertificate", "Fail",
                    targetId: id.ToString(), reason: $"Exception: {ex.Message}");
                return BadRequest($"Eroare: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshCertificate(int id)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var certificate = await _context.ManagedCertificates.FindAsync(id);
                if (certificate == null)
                    return NotFound();

                // Verifică certificatul(ele) din nou (dual verification dacă IP disponibil)
                var checkResult = await _certificateCheckService.CheckCertificateAsync(
                    certificate.Url,
                    certificate.OriginServerIP,
                    certificate.OriginServerPort);

                certificate.Status = checkResult.Status;
                certificate.CertificateExpiryDate = checkResult.CertificateExpiryDate;
                certificate.DaysUntilExpiry = checkResult.DaysUntilExpiry;
                certificate.CertificateSubject = checkResult.CertificateSubject;
                certificate.CertificateIssuer = checkResult.CertificateIssuer;
                certificate.ErrorMessage = checkResult.ErrorMessage;
                certificate.LastCheckDate = checkResult.LastCheckDate;

                // Update origin certificate info
                certificate.OriginCertificateExpiryDate = checkResult.OriginCertificateExpiryDate;
                certificate.OriginCertificateSubject = checkResult.OriginCertificateSubject;
                certificate.OriginCertificateIssuer = checkResult.OriginCertificateIssuer;
                certificate.VerificationMethod = checkResult.VerificationMethod;
                certificate.IsCertificateMismatch = checkResult.IsCertificateMismatch;
                certificate.OriginDaysUntilExpiry = checkResult.OriginDaysUntilExpiry;

                _context.ManagedCertificates.Update(certificate);
                await _context.SaveChangesAsync();

                _audit.Log(HttpContext, "CertificateManagement.RefreshCertificate", "Success",
                    targetId: id.ToString(), targetType: "Certificate");

                return Ok(new { success = true, data = certificate });
            }
            catch (Exception ex)
            {
                _audit.Log(HttpContext, "CertificateManagement.RefreshCertificate", "Fail",
                    targetId: id.ToString(), reason: $"Exception: {ex.Message}");
                return BadRequest($"Eroare: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificateStatus(int id)
        {
            if (!IsAdmin())
                return Forbid();

            var certificate = await _context.ManagedCertificates.FindAsync(id);
            if (certificate == null)
                return NotFound();

            return Ok(certificate);
        }
    }
}
