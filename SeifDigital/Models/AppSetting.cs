using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class AppSetting
    {
        [Key]
        [MaxLength(128)]
        public string Key { get; set; } = "";

        // EXISTENT – folosit deja (audit retention etc.)
        [MaxLength(1024)]
        public string? Value { get; set; }

        // NOU – pentru setări text mai mari (extensii fișiere, JSON, liste etc.)
        [MaxLength(2000)]
        public string? ValueString { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
