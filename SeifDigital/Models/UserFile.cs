using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserFile
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string OwnerUser { get; set; } = "";   // DOMAIN\user

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Extension { get; set; } = ""; // ".pfx", ".cer" etc.

        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = ""; // ex: GUID + ext

        public long SizeBytes { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
    }
}
