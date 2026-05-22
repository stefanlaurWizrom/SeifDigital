using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    public class CertificateAlertLog
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Reference to the managed certificate
        /// </summary>
        [Required]
        public int ManagedCertificateId { get; set; }

        [ForeignKey("ManagedCertificateId")]
        public virtual ManagedCertificate? ManagedCertificate { get; set; }

        /// <summary>
        /// Certificate URL (denormalized for easier querying)
        /// </summary>
        [Required]
        [StringLength(2048)]
        public string CertificateUrl { get; set; } = string.Empty;

        /// <summary>
        /// Days until expiry when alert was sent
        /// </summary>
        [Required]
        public int DaysUntilExpiry { get; set; }

        /// <summary>
        /// Alert type: "Expiring Soon" or "Expired"
        /// </summary>
        [Required]
        [StringLength(50)]
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Email address where alert was sent
        /// </summary>
        [Required]
        [StringLength(256)]
        public string EmailSentTo { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when alert was sent
        /// </summary>
        [Required]
        public DateTime AlertSentDateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Email delivery status: "Sent", "Failed", "Pending"
        /// </summary>
        [Required]
        [StringLength(20)]
        public string EmailStatus { get; set; } = "Pending";

        /// <summary>
        /// Error message if email failed
        /// </summary>
        [StringLength(1024)]
        public string? ErrorMessage { get; set; }
    }
}
