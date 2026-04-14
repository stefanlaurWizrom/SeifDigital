using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    [Table("NoteFisier", Schema = "dbo")]
    public class NoteFisier
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long UserNote_Id { get; set; }

        [Required]
        public long UserFile_Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string FileType { get; set; } = ""; // "image", "document", "certificate"

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Foreign keys - Navigation properties
        [ForeignKey(nameof(UserNote_Id))]
        public UserNote? UserNote { get; set; }

        [ForeignKey(nameof(UserFile_Id))]
        public UserFile? UserFile { get; set; }
    }
}
