using System.ComponentModel.DataAnnotations;

namespace SeifDigital.Models
{
    public class InformatieSensibilaEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string TitluAplicatie { get; set; } = "";

        [Required]
        [StringLength(200)]
        public string UsernameSalvat { get; set; } = "";

        [Required]
        [StringLength(500)]
        public string Parola { get; set; } = "";

        [StringLength(4000)]
        public string? Detalii { get; set; }
    }
}
