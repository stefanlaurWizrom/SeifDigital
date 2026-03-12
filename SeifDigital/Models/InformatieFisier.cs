using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    [Table("InformatieFisier", Schema = "dbo")]
    public class InformatieFisier
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int InformatieSensibila_Id { get; set; }

        [Required]
        public long UserFile_Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string FileType { get; set; } = ""; // "image", "document", "certificate"

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Foreign keys - Navigation properties
        [ForeignKey(nameof(InformatieSensibila_Id))]
        public InformatieSensibila? InformatieSensibila { get; set; }

        [ForeignKey(nameof(UserFile_Id))]
        public UserFile? UserFile { get; set; }
    }
}
