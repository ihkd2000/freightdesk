using System.ComponentModel.DataAnnotations;

namespace FreightDesk.Models
{
    /// <summary>One outbound email attempt. Bodies are kept so a failed or lost email can be re-sent.</summary>
    public class EmailLog
    {
        public int Id { get; set; }

        public DateTime SentAtUtc { get; set; }

        [MaxLength(200)]
        public string ToAddress { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Kind { get; set; } = string.Empty;

        public bool Success { get; set; }

        [MaxLength(500)]
        public string? Error { get; set; }

        public string TextBody { get; set; } = string.Empty;

        public string HtmlBody { get; set; } = string.Empty;

        /// <summary>The shipment this email was about (no foreign key: shipments are soft-deleted).</summary>
        public int? ContainerId { get; set; }
    }
}
