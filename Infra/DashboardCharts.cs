using System.Globalization;
using FreightDesk.Models;

namespace FreightDesk.Infra
{
    /// <summary>One column of a two-part stacked column chart: the settled part and the open part.</summary>
    public record StackedColumn(string Label, string LongLabel, decimal Settled, decimal Open)
    {
        public decimal Total => Settled + Open;
    }

    public record CarrierShare(string Name, int Count);

    /// <summary>Numbers behind the dashboard charts. Pure logic over loaded shipments so it is easy to test.</summary>
    public static class DashboardCharts
    {
        /// <summary>
        /// Open (not released) shipments arriving in each of the next <paramref name="weeks"/> weeks, starting with
        /// the current Monday-to-Sunday week, split into paid and unpaid.
        /// </summary>
        public static IReadOnlyList<StackedColumn> ArrivalsByWeek(IEnumerable<Shipment> shipments, DateTime today, int weeks = 8)
        {
            var firstMonday = StartOfWeek(today.Date);
            var open = shipments.Where(s => s.Deleted != 1 && s.ReleaseDate == null && s.Arrival.HasValue).ToList();

            return Enumerable.Range(0, weeks).Select(i =>
            {
                var start = firstMonday.AddDays(7 * i);
                var inWeek = open.Where(s => s.Arrival!.Value.Date >= start && s.Arrival.Value.Date < start.AddDays(7)).ToList();
                var paid = inWeek.Count(s => s.PaymentReceivedDate.HasValue);
                return new StackedColumn(
                    i == 0 ? "This wk" : start.ToString("d MMM", CultureInfo.InvariantCulture),
                    i == 0 ? $"This week ({start:d MMM} – {start.AddDays(6):d MMM})" : $"Week of {start:d MMM} – {start.AddDays(6):d MMM}",
                    paid,
                    inWeek.Count - paid);
            }).ToList();
        }

        /// <summary>
        /// Invoiced amounts for the last <paramref name="months"/> calendar months (this month last), by invoice date,
        /// split into received and still outstanding. Shipments without an invoice date or price are left out.
        /// </summary>
        public static IReadOnlyList<StackedColumn> BillingByMonth(IEnumerable<Shipment> shipments, DateTime today, int months = 6)
        {
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            var invoiced = shipments.Where(s => s.Deleted != 1 && s.InvoiceDate.HasValue && s.Price.HasValue).ToList();

            return Enumerable.Range(0, months).Select(i =>
            {
                var month = thisMonth.AddMonths(i - months + 1);
                var inMonth = invoiced.Where(s => s.InvoiceDate!.Value.Year == month.Year && s.InvoiceDate.Value.Month == month.Month).ToList();
                var received = inMonth.Where(s => s.PaymentReceivedDate.HasValue).Sum(s => s.Price!.Value);
                var outstanding = inMonth.Where(s => !s.PaymentReceivedDate.HasValue).Sum(s => s.Price!.Value);
                return new StackedColumn(
                    month.ToString("MMM", CultureInfo.InvariantCulture),
                    month.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    received,
                    outstanding);
            }).ToList();
        }

        /// <summary>Open shipments per carrier, largest first; carriers past <paramref name="top"/> fold into "Other".</summary>
        public static IReadOnlyList<CarrierShare> ByCarrier(IEnumerable<Shipment> shipments, int top = 6)
        {
            var counts = shipments
                .Where(s => s.Deleted != 1 && s.ReleaseDate == null)
                .GroupBy(s => s.Carrier?.Name ?? "No carrier")
                .Select(g => new CarrierShare(g.Key, g.Count()))
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (counts.Count <= top)
            {
                return counts;
            }

            // Keep top - 1 named carriers so the list, including "Other", is never longer than top.
            var shown = counts.Take(top - 1).ToList();
            shown.Add(new CarrierShare($"Other ({counts.Count - shown.Count} carriers)", counts.Skip(top - 1).Sum(c => c.Count)));
            return shown;
        }

        /// <summary>
        /// A round axis maximum at or above <paramref name="max"/>: 1, 2, 4, 6 or 8 times a power of ten (at least 2), so the
        /// halfway tick is a whole number too (counts never get a "2.5 shipments" gridline).
        /// </summary>
        public static decimal NiceMax(decimal max)
        {
            if (max <= 1) return 2;
            var power = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)max)));
            foreach (var step in new[] { 1m, 2m, 4m, 6m, 8m, 10m })
            {
                if (step * power >= max) return step * power;
            }
            return 20 * power;
        }

        private static DateTime StartOfWeek(DateTime date) =>
            date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }
}
