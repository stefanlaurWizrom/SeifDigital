using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserAccount
    {
        public long Id { get; set; }

        [Required, MaxLength(256)]
        public string Email { get; set; } = ""; // lower-case

        [Required]
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

        [Required]
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; } = false;

    }
}
