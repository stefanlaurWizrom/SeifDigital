using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    public class InformatieSensibila
    {
        public int Id { get; set; }

        public string? NumeUtilizator { get; set; }
        public string? UsernameSalvat { get; set; }
        public string? TitluAplicatie { get; set; }
        public string? OwnerKey { get; set; }

        public string? DateCriptate { get; set; }
        public string? DetaliiCriptate { get; set; }
        public string? DetaliiTokens { get; set; }

        public DateTime? LastUpdatedUtc { get; set; }

        // ✅ ACTUALIZAT: Many-to-Many relație cu imagini
        public ICollection<InformatieImagine> Imagini { get; set; } = new List<InformatieImagine>();

        [NotMapped]
        public string? ParolaDecriptata { get; set; }
    }
}
