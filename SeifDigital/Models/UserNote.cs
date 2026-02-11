using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class UserNote
    {
        public long Id { get; set; }   // BIGINT în SQL => long în C#

        [MaxLength(256)]
        public string? OwnerKey { get; set; }

        [Required]
        [MaxLength(256)]
        public string OwnerUser { get; set; } = "";

        // ✅ NOU
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = "Fără titlu";

        [Required]
        [MaxLength(255)]
        public string Text { get; set; } = ""; // mapat în DB la NoteText

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
