using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    public class InformatieSensibila
    {
        public int Id { get; set; }

        public string? NumeUtilizator { get; set; }     // OWNER (user domeniu logat)
        public string? UsernameSalvat { get; set; }     // user pentru RDP/aplicație (EDITABIL)
        public string? TitluAplicatie { get; set; }     // titlu

        public string? DateCriptate { get; set; }       // parola criptată (string)
        public string? DetaliiCriptate { get; set; }    // detalii criptate (string)  <-- NOU

        // tokenized, normalized text used for searching (NOT full details)
        public string? DetaliiTokens { get; set; }

        [NotMapped]
        public string? ParolaDecriptata { get; set; }   // DOAR pentru afișare (nu se salvează)
    }
}
