using System;

namespace SeifDigital.Models
{
    public class UserMessage
    {
        public long Id { get; set; }

        public string RecipientOwnerKey { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public string SourceType { get; set; } = ""; // "Parole" / "Notes"

        public long? OriginalId { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? SavedUtc { get; set; }

        // ===== payload pentru Parole =====
        public string? TitluAplicatie { get; set; }
        public string? UsernameSalvat { get; set; }
        public string? DateCriptate { get; set; }
        public string? DetaliiCriptate { get; set; }
        public string? DetaliiTokens { get; set; }

        // ===== payload pentru Notes =====
        public string? NoteText { get; set; }
        public string? Text { get; set; } // opțional
    }
}
