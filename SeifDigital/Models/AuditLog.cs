using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SeifDigital.Models
{
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        public DateTime EventTimeUtc { get; set; }

        [MaxLength(64)]
        public string EventType { get; set; } = "";

        [MaxLength(256)]
        public string ActorUser { get; set; } = "";

        [MaxLength(128)]
        public string? ActorSid { get; set; }

        [MaxLength(64)]
        public string? TargetType { get; set; }

        [MaxLength(64)]
        public string? TargetId { get; set; }

        [MaxLength(16)]
        public string Outcome { get; set; } = "Success"; // Success / Fail

        [MaxLength(256)]
        public string? Reason { get; set; }

        [MaxLength(64)]
        public string? ClientIp { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [MaxLength(64)]
        public string? CorrelationId { get; set; }

        public string? DetailsJson { get; set; }
    }
}
