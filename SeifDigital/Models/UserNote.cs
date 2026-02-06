using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserNote
    {
        public long Id { get; set; }   // BIGINT în SQL => long în C#

        // OwnerKey = email (unifică Windows + Mac)
        [MaxLength(256)]
        public string? OwnerKey { get; set; }

        // păstrăm OwnerUser pentru istoric (DOMAIN\user)
        [Required]
        [MaxLength(256)]
        public string OwnerUser { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string Text { get; set; } = ""; // mapat în DB la NoteText

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
