using FreightDesk.Models;

namespace FreightDesk.Infra
{
    public enum AttentionReason
    {
        PaymentOverdue,
        UnpaidArrivingSoon,
        ReadyToRelease,
        MissingClientEmail
    }

    /// <summary>Decides which shipments need a person to act. Pure logic so it is easy to test.</summary>
    public static class AttentionRules
    {
        public static IReadOnlyList<AttentionReason> Evaluate(Shipment s, DateTime today, int windowDays)
        {
            var reasons = new List<AttentionReason>();
            if (s.Deleted == 1 || s.ReleaseDate.HasValue)
            {
                return reasons;
            }

            var unpaid = !s.PaymentReceivedDate.HasValue;
            var hasArrival = s.Arrival.HasValue;
            var arrival = s.Arrival?.Date ?? DateTime.MaxValue;

            if (unpaid && hasArrival && arrival < today.Date)
            {
                reasons.Add(AttentionReason.PaymentOverdue);
            }
            else if (unpaid && hasArrival && arrival <= today.Date.AddDays(windowDays))
            {
                reasons.Add(AttentionReason.UnpaidArrivingSoon);
            }

            if (!unpaid && hasArrival && arrival <= today.Date)
            {
                reasons.Add(AttentionReason.ReadyToRelease);
            }

            if (string.IsNullOrWhiteSpace(s.Client?.Email))
            {
                reasons.Add(AttentionReason.MissingClientEmail);
            }

            return reasons;
        }

        public static string Label(this AttentionReason reason) => reason switch
        {
            AttentionReason.PaymentOverdue => "Payment overdue",
            AttentionReason.UnpaidArrivingSoon => "Unpaid, arriving soon",
            AttentionReason.ReadyToRelease => "Paid, ready to release",
            _ => "Client has no email"
        };

        public static string CssClass(this AttentionReason reason) => reason switch
        {
            AttentionReason.PaymentOverdue => "status-overdue",
            AttentionReason.UnpaidArrivingSoon => "status-awaiting",
            AttentionReason.ReadyToRelease => "status-released",
            _ => "status-planned"
        };
    }
}
