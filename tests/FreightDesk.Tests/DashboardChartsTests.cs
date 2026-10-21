using FreightDesk.Infra;
using FreightDesk.Models;

namespace FreightDesk.Tests;

public class DashboardChartsTests
{
    // A Wednesday, so the current week runs Monday 19 to Sunday 25 October.
    private static readonly DateTime Today = new(2026, 10, 21);

    private static Shipment S(DateTime? arrival = null, bool paid = false, bool released = false, bool deleted = false,
        DateTime? invoiced = null, decimal? price = null, string? carrier = null) => new()
    {
        Arrival = arrival,
        PaymentReceivedDate = paid ? Today : null,
        ReleaseDate = released ? Today : null,
        Deleted = deleted ? 1 : 0,
        InvoiceDate = invoiced,
        Price = price,
        Carrier = carrier == null ? null! : new Carrier { Name = carrier }
    };

    [Fact]
    public void Arrivals_are_bucketed_into_monday_weeks_split_by_payment()
    {
        var weeks = DashboardCharts.ArrivalsByWeek(new[]
        {
            S(new DateTime(2026, 10, 19)),               // Monday of this week
            S(new DateTime(2026, 10, 25), paid: true),   // Sunday of this week
            S(new DateTime(2026, 10, 26)),               // next Monday
            S(new DateTime(2026, 12, 13)),               // last day of week 8
            S(new DateTime(2026, 12, 14)),               // week 9: outside
            S(new DateTime(2026, 10, 18)),               // last week: outside
            S(new DateTime(2026, 10, 20), released: true),
            S(new DateTime(2026, 10, 20), deleted: true),
            S(null)
        }, Today);

        Assert.Equal(8, weeks.Count);
        Assert.Equal(("This wk", 1m, 1m), (weeks[0].Label, weeks[0].Settled, weeks[0].Open));
        Assert.Equal(("26 Oct", 0m, 1m), (weeks[1].Label, weeks[1].Settled, weeks[1].Open));
        Assert.Equal(1m, weeks[7].Total);
        Assert.Equal(4m, weeks.Sum(w => w.Total));
    }

    [Fact]
    public void Billing_sums_prices_by_invoice_month_split_by_received()
    {
        var months = DashboardCharts.BillingByMonth(new[]
        {
            S(invoiced: new DateTime(2026, 10, 2), price: 1000m, paid: true),
            S(invoiced: new DateTime(2026, 10, 30), price: 500m),
            S(invoiced: new DateTime(2026, 5, 1), price: 200m),   // first of the 6 months
            S(invoiced: new DateTime(2026, 4, 30), price: 999m),  // too old
            S(invoiced: new DateTime(2026, 10, 3), price: null),  // no price
            S(invoiced: null, price: 700m),                       // no invoice date
            S(invoiced: new DateTime(2026, 10, 4), price: 300m, deleted: true)
        }, Today);

        Assert.Equal(new[] { "May", "Jun", "Jul", "Aug", "Sep", "Oct" }, months.Select(m => m.Label));
        Assert.Equal((1000m, 500m), (months[^1].Settled, months[^1].Open));
        Assert.Equal(200m, months[0].Open);
        Assert.Equal(1700m, months.Sum(m => m.Total));
    }

    [Fact]
    public void Carriers_are_ranked_and_the_tail_folds_into_other()
    {
        var shipments = new List<Shipment>();
        void Add(string carrier, int n) { for (var i = 0; i < n; i++) shipments.Add(S(carrier: carrier)); }
        Add("Maersk", 5); Add("MSC", 4); Add("CMA CGM", 3); Add("Hapag-Lloyd", 2); Add("ONE", 1); Add("Zim", 1); Add("Cosco", 1);
        shipments.Add(S(carrier: "Maersk", released: true));
        shipments.Add(S());

        var bars = DashboardCharts.ByCarrier(shipments, top: 6);

        Assert.Equal(
            new[] { ("Maersk", 5), ("MSC", 4), ("CMA CGM", 3), ("Hapag-Lloyd", 2), ("Cosco", 1), ("Other (3 carriers)", 3) },
            bars.Select(b => (b.Name, b.Count)));
    }

    [Fact]
    public void Few_carriers_are_not_folded()
    {
        var bars = DashboardCharts.ByCarrier(new[] { S(carrier: "Maersk"), S(carrier: "MSC"), S(carrier: "Maersk") });
        Assert.Equal(new[] { ("Maersk", 2), ("MSC", 1) }, bars.Select(b => (b.Name, b.Count)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(7, 8)]
    [InlineData(10, 10)]
    [InlineData(11, 20)]
    [InlineData(1700, 2000)]
    [InlineData(3500, 4000)]
    [InlineData(8200, 10000)]
    public void Axis_maximum_is_round_with_a_whole_midpoint(int max, int expected)
    {
        var nice = DashboardCharts.NiceMax(max);
        Assert.Equal(expected, nice);
        Assert.Equal(0, nice / 2 % 1);
    }
}
