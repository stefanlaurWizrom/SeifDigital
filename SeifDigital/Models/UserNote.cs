using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    public class UserNote
    {
        public long Id { get; set; }

        [MaxLength(256)]
        public string? OwnerKey { get; set; }

        [Required]
        [MaxLength(256)]
        public string OwnerUser { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = "Fără titlu";

        // ✅ DOAR ACEASTA COLOANĂ - Text criptat direct în DB
        // Se va mapează la coloana "NoteText" din database
        [Required]
        [Column("NoteText")]
        public string Text { get; set; } = "";

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        // ✅ NOU: Many-to-Many relație cu fișiere
        public ICollection<NoteFisier> Fisieri { get; set; } = new List<NoteFisier>();
    }
}
