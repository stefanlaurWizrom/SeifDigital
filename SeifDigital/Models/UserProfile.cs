using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserProfile
    {
        [Key]
        public long Id { get; set; }

        [MaxLength(256)]
        public string DomainUser { get; set; } = "";

        [MaxLength(256)]
        public string Email { get; set; } = "";

        [MaxLength(32)]
        public string EmailSource { get; set; } = "AD"; // AD / CACHE

        public DateTime LastVerifiedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    }
}
