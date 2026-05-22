using System;
using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class ManagedCertificate
    {
        public int Id { get; set; }

        /// <summary>
        /// URL-ul site-ului pentru care verificăm certificatul
        /// </summary>
        [Required]
        [StringLength(2048)]
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// Data expirării certificatului
        /// </summary>
        public DateTime? CertificateExpiryDate { get; set; }
        
        /// <summary>
        /// Zilele rămase până la expirare
        /// </summary>
        public int? DaysUntilExpiry { get; set; }

        /// <summary>
        /// Status: Valid, Expiring Soon, Expired, Error
        /// </summary>
        [StringLength(50)]
        public string Status { get; set; } = "Unknown";
        
        /// <summary>
        /// Mesaj de eroare dacă verificarea a eșuat
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Data ultimei verificări
        /// </summary>
        public DateTime? LastCheckDate { get; set; }
        
        /// <summary>
        /// Data adăugării certificatului în sistem
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Subiectul certificatului (CN)
        /// </summary>
        public string? CertificateSubject { get; set; }

        /// <summary>
        /// Emitentul certificatului
        /// </summary>
        public string? CertificateIssuer { get; set; }

        /// <summary>
        /// IP-ul serverului de origine (pentru verificare directă)
        /// </summary>
        public string? OriginServerIP { get; set; }

        /// <summary>
        /// Portul serverului de origine (implicit 443)
        /// </summary>
        public int OriginServerPort { get; set; } = 443;

        /// <summary>
        /// Data expirării certificatului de origine (direct)
        /// </summary>
        public DateTime? OriginCertificateExpiryDate { get; set; }

        /// <summary>
        /// Subiectul certificatului de origine
        /// </summary>
        public string? OriginCertificateSubject { get; set; }

        /// <summary>
        /// Emitentul certificatului de origine
        /// </summary>
        public string? OriginCertificateIssuer { get; set; }

        /// <summary>
        /// Metoda de verificare: "URL" (CDN), "Direct" (Origin IP), sau "Both"
        /// </summary>
        public string? VerificationMethod { get; set; }

        /// <summary>
        /// Flag pentru alert: certificatul de pe URL diferă de cel de pe origin IP
        /// </summary>
        public bool IsCertificateMismatch { get; set; }

        /// <summary>
        /// Zilele rămase până la expirare pentru certificatul de origine
        /// </summary>
        public int? OriginDaysUntilExpiry { get; set; }
    }
}
