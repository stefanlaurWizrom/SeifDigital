using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace SeifDigital.Services
{
    public class CertificateCheckService
    {
        private readonly int _defaultTimeout = 10000; // 10 seconds

        /// <summary>
        /// Verifică certificatul SSL/TLS al unui URL cu opțiune de verificare directă a IP-ului de origine
        /// </summary>
        public async Task<CertificateCheckResult> CheckCertificateAsync(
            string url, 
            string? originIP = null, 
            int originPort = 443)
        {
            var result = new CertificateCheckResult();

            try
            {
                // Verifică certificatul de pe URL (CDN/proxy)
                var urlCertResult = await CheckCertificateFromUrlAsync(url);

                result.CertificateSubject = urlCertResult.CertificateSubject;
                result.CertificateIssuer = urlCertResult.CertificateIssuer;
                result.CertificateExpiryDate = urlCertResult.CertificateExpiryDate;
                result.DaysUntilExpiry = urlCertResult.DaysUntilExpiry;
                result.Status = urlCertResult.Status;
                result.ErrorMessage = urlCertResult.ErrorMessage;
                result.LastCheckDate = DateTime.UtcNow;
                result.IsSuccess = urlCertResult.IsSuccess;
                result.VerificationMethod = "URL";

                // Dacă este oferit IP de origine, verifică direct
                if (!string.IsNullOrWhiteSpace(originIP) && IsValidIP(originIP))
                {
                    var originCertResult = await CheckCertificateFromIPAsync(url, originIP, originPort);

                    result.OriginCertificateSubject = originCertResult.CertificateSubject;
                    result.OriginCertificateIssuer = originCertResult.CertificateIssuer;
                    result.OriginCertificateExpiryDate = originCertResult.CertificateExpiryDate;
                    result.OriginDaysUntilExpiry = originCertResult.DaysUntilExpiry;

                    // Compară certificatele
                    result.IsCertificateMismatch = CompareCertificates(
                        urlCertResult.CertificateSubject,
                        originCertResult.CertificateSubject);

                    // Status este determinat de certificatul de origine (authoritative)
                    result.Status = originCertResult.Status;
                    result.DaysUntilExpiry = originCertResult.DaysUntilExpiry;
                    result.CertificateExpiryDate = originCertResult.CertificateExpiryDate;
                    result.ErrorMessage = originCertResult.ErrorMessage;
                    result.IsSuccess = originCertResult.IsSuccess;
                    result.VerificationMethod = "Both";
                }

                result.LastCheckDate = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = $"Eroare la verificarea certificatului: {ex.Message}";
                result.IsSuccess = false;
                result.LastCheckDate = DateTime.UtcNow;
                return result;
            }
        }

        /// <summary>
        /// Verifică certificatul SSL/TLS al unui URL (doar metoda originală)
        /// </summary>
        public async Task<CertificateCheckResult> CheckCertificateAsync(string url)
        {
            return await CheckCertificateFromUrlAsync(url);
        }

        /// <summary>
        /// Verifică certificatul de pe un URL (CDN/proxy)
        /// </summary>
        private async Task<CertificateCheckResult> CheckCertificateFromUrlAsync(string url)
        {
            var result = new CertificateCheckResult();

            try
            {
                // Extract hostname și port din URL
                var uri = new Uri(url);
                string hostname = uri.Host;
                int port = uri.Port;

                // Setează port-ul implicit dacă nu este specificat
                if (port == -1)
                {
                    port = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
                }

                // Dacă nu e HTTPS, nu putem verifica certificatul
                if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "Error";
                    result.ErrorMessage = "URL-ul trebuie să fie HTTPS pentru a verifica certificatul.";
                    return result;
                }

                using (var socket = new TcpClient())
                {
                    // Conectează-te la server cu timeout
                    var connectTask = socket.ConnectAsync(hostname, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(_defaultTimeout)) == connectTask)
                    {
                        await connectTask;
                    }
                    else
                    {
                        throw new TimeoutException($"Timeout connecting to {hostname}:{port}");
                    }

                    using (var sslStream = new SslStream(socket.GetStream(), false))
                    {
                        // Authentificare SSL cu SNI
                        var sslOptions = new SslClientAuthenticationOptions
                        {
                            TargetHost = hostname,
                            RemoteCertificateValidationCallback = (s, c, ch, e) => true
                        };

                        await sslStream.AuthenticateAsClientAsync(sslOptions);

                        // Obține certificatul
                        var certificate = new X509Certificate2(sslStream.RemoteCertificate.Export(X509ContentType.Cert));

                        // Extrage informații
                        result.CertificateSubject = certificate.Subject;
                        result.CertificateIssuer = certificate.Issuer;
                        result.CertificateExpiryDate = certificate.NotAfter;
                        result.CertificateNotBefore = certificate.NotBefore;

                        // Calculează zilele rămase
                        TimeSpan timeUntilExpiry = certificate.NotAfter - DateTime.UtcNow;
                        result.DaysUntilExpiry = (int)timeUntilExpiry.TotalDays;

                        // Setează status
                        if (certificate.NotAfter < DateTime.UtcNow)
                        {
                            result.Status = "Expired";
                            result.ErrorMessage = $"Certificatul a expirat pe {certificate.NotAfter:yyyy-MM-dd}";
                        }
                        else if (result.DaysUntilExpiry <= 30)
                        {
                            result.Status = "Expiring Soon";
                            result.ErrorMessage = $"Certificatul va expira în {result.DaysUntilExpiry} zile";
                        }
                        else
                        {
                            result.Status = "Valid";
                        }

                        result.LastCheckDate = DateTime.UtcNow;
                        result.IsSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = $"Eroare la verificarea certificatului: {ex.Message}";
                result.IsSuccess = false;
            }

            return result;
        }

        /// <summary>
        /// Verifică certificatul de pe IP-ul origin (direct connection)
        /// </summary>
        private async Task<CertificateCheckResult> CheckCertificateFromIPAsync(string url, string originIP, int originPort)
        {
            var result = new CertificateCheckResult();

            try
            {
                var uri = new Uri(url);
                string hostname = uri.Host;

                using (var socket = new TcpClient())
                {
                    // Conectează-te direct la IP cu timeout
                    var connectTask = socket.ConnectAsync(originIP, originPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(_defaultTimeout)) == connectTask)
                    {
                        await connectTask;
                    }
                    else
                    {
                        throw new TimeoutException($"Timeout connecting to {originIP}:{originPort}");
                    }

                    using (var sslStream = new SslStream(socket.GetStream(), false))
                    {
                        // Authentificare SSL cu SNI (folosind hostname din URL)
                        var sslOptions = new SslClientAuthenticationOptions
                        {
                            TargetHost = hostname,
                            RemoteCertificateValidationCallback = (s, c, ch, e) => true
                        };

                        await sslStream.AuthenticateAsClientAsync(sslOptions);

                        // Obține certificatul
                        var certificate = new X509Certificate2(sslStream.RemoteCertificate.Export(X509ContentType.Cert));

                        // Extrage informații
                        result.CertificateSubject = certificate.Subject;
                        result.CertificateIssuer = certificate.Issuer;
                        result.CertificateExpiryDate = certificate.NotAfter;
                        result.CertificateNotBefore = certificate.NotBefore;

                        // Calculează zilele rămase
                        TimeSpan timeUntilExpiry = certificate.NotAfter - DateTime.UtcNow;
                        result.DaysUntilExpiry = (int)timeUntilExpiry.TotalDays;

                        // Setează status (origin cert is authoritative)
                        if (certificate.NotAfter < DateTime.UtcNow)
                        {
                            result.Status = "Expired";
                            result.ErrorMessage = $"Certificatul de origine a expirat pe {certificate.NotAfter:yyyy-MM-dd}";
                        }
                        else if (result.DaysUntilExpiry <= 30)
                        {
                            result.Status = "Expiring Soon";
                            result.ErrorMessage = $"Certificatul de origine va expira în {result.DaysUntilExpiry} zile";
                        }
                        else
                        {
                            result.Status = "Valid";
                        }

                        result.LastCheckDate = DateTime.UtcNow;
                        result.IsSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.ErrorMessage = $"Eroare la verificarea certificatului de origine: {ex.Message}";
                result.IsSuccess = false;
            }

            return result;
        }

        /// <summary>
        /// Validează dacă șirul este o adresă IP validă (IPv4 sau IPv6)
        /// </summary>
        private bool IsValidIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;

            return System.Net.IPAddress.TryParse(ip, out _);
        }

        /// <summary>
        /// Compară două certificatele (Subject = DN)
        /// </summary>
        private bool CompareCertificates(string? subject1, string? subject2)
        {
            if (string.IsNullOrEmpty(subject1) || string.IsNullOrEmpty(subject2))
                return false;

            // Simple comparison - could be enhanced for more detailed matching
            return !subject1.Equals(subject2, StringComparison.Ordinal);
        }

    }

    public class CertificateCheckResult
    {
        public bool IsSuccess { get; set; }
        public string Status { get; set; } // Valid, Expiring Soon, Expired, Error
        public DateTime? CertificateExpiryDate { get; set; }
        public DateTime? CertificateNotBefore { get; set; }
        public int? DaysUntilExpiry { get; set; }
        public string CertificateSubject { get; set; }
        public string CertificateIssuer { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? LastCheckDate { get; set; }

        // Origin certificate fields (from direct IP verification)
        public string? OriginCertificateSubject { get; set; }
        public string? OriginCertificateIssuer { get; set; }
        public DateTime? OriginCertificateExpiryDate { get; set; }
        public int? OriginDaysUntilExpiry { get; set; }

        // Verification metadata
        public string? VerificationMethod { get; set; }  // URL, Direct, Both
        public bool IsCertificateMismatch { get; set; }
    }
}
