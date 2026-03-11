using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserFile
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string OwnerUser { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Extension { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = "";

        [MaxLength(255)]
        public string StoredRelativePath { get; set; } = "";

        public long SizeBytes { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;

        // ✅ NOU: Many-to-Many relație cu InformatiiSensibile
        public ICollection<InformatieImagine> InformatiiSensibile { get; set; } = new List<InformatieImagine>();
    }
}
