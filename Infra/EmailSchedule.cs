namespace FreightDesk.Infra
{
    public record PlannedEmail(string Kind, DateTime Date, string Note);

    /// <summary>
    /// When the background jobs will email a client about a shipment. Shared by the jobs (settings) and the
    /// New shipment page (preview), so the two cannot drift apart. Pure logic so it is easy to test.
    /// </summary>
    public static class EmailSchedule
    {
        /// <summary>ArrivalNotifications:DaysBefore, default 10, at least 1.</summary>
        public static int ArrivalDaysBefore(IConfiguration configuration) =>
            Math.Max(1, configuration.GetValue("ArrivalNotifications:DaysBefore", 10));

        /// <summary>PaymentReminders:DaysBefore, default [5, 2]; distinct, non-negative, largest first.</summary>
        public static int[] ReminderOffsets(IConfiguration configuration)
        {
            var configured = configuration.GetSection("PaymentReminders:DaysBefore").Get<int[]>();
            return (configured is { Length: > 0 } ? configured : new[] { 5, 2 })
                .Where(d => d >= 0).Distinct().OrderByDescending(d => d).ToArray();
        }

        /// <summary>
        /// Emails still to come for an unpaid shipment arriving on <paramref name="arrival"/>, in date order.
        /// Mirrors the jobs: nothing is sent once the arrival date has passed, the arrival notice goes out once
        /// inside its window, and reminder stages that are already due are combined into a single email.
        /// </summary>
        public static IReadOnlyList<PlannedEmail> Plan(DateTime? arrival, DateTime today, int arrivalDaysBefore,
            IReadOnlyList<int> reminderOffsets)
        {
            var planned = new List<PlannedEmail>();
            if (arrival == null || arrival.Value.Date < today.Date)
            {
                return planned;
            }

            var arrivalDate = arrival.Value.Date;
            var noticeDate = Max(today.Date, arrivalDate.AddDays(-arrivalDaysBefore));
            planned.Add(new PlannedEmail(EmailKinds.ArrivalNotice, noticeDate,
                noticeDate == today.Date ? "Arrival is inside the notice window, so it goes out right away" : $"{arrivalDaysBefore} days before arrival"));

            var dueNow = reminderOffsets.Where(o => arrivalDate.AddDays(-o) <= today.Date).ToList();
            if (dueNow.Count > 0)
            {
                planned.Add(new PlannedEmail(EmailKinds.PaymentReminder, today.Date,
                    "Already inside the reminder window, so it goes out right away"));
            }

            foreach (var offset in reminderOffsets.Except(dueNow))
            {
                planned.Add(new PlannedEmail(EmailKinds.PaymentReminder, arrivalDate.AddDays(-offset),
                    offset == 1 ? "1 day before arrival, if still unpaid" : $"{offset} days before arrival, if still unpaid"));
            }

            return planned.OrderBy(p => p.Date).ToList();
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
