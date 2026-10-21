using Bunit;
using FreightDesk.Data;
using FreightDesk.Infra;
using FreightDesk.Models;
using FreightDesk.Pages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreightDesk.Tests;

public sealed class DashboardPageTests : BunitContext
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public DashboardPageTests()
    {
        _connection.Open();
        Services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(Options.Create(new BrandingOptions { CompanyName = "Test Freight" }));

        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
        db.Clients.Add(new Client { Id = 1, Name = "ABC Shipping", Email = "abc@example.com" });
        db.Carriers.AddRange(new Carrier { Id = 1, Name = "Northwind Lines" }, new Carrier { Id = 2, Name = "Bluewave Shipping" });
        db.Ports.Add(new Port { Id = 1, Name = "New York" });

        var n = 0;
        Shipment Add(int carrier, int arrivalInDays, bool paid = false, bool released = false) => new()
        {
            Owner = "owner", JobReferenceNumber = "J", BookingNumber = "B" + ++n, ContainerNumber = "C" + n,
            CarrierId = carrier, PortId = 1, ClientId = 1,
            Arrival = DateTime.Today.AddDays(arrivalInDays),
            InvoiceDate = DateTime.Today, Price = 1000m,
            PaymentReceivedDate = paid ? DateTime.Today : null,
            ReleaseDate = released ? DateTime.Today : null
        };
        db.Shipments.AddRange(
            Add(1, 3), Add(1, 3, paid: true), Add(2, 10),
            Add(1, -5, paid: true, released: true));
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }

    private static string Stat(IRenderedComponent<Dashboard> page, string label) =>
        page.FindAll(".stat-card").Single(c => c.QuerySelector(".stat-label")!.TextContent.StartsWith(label))
            .QuerySelector(".stat-value")!.TextContent.Trim();

    [Fact]
    public void Active_shipments_leave_out_released_ones()
    {
        var page = Render<Dashboard>();

        Assert.Equal("3", Stat(page, "Active shipments"));
        Assert.Equal("1", Stat(page, "Released"));
    }

    [Fact]
    public void Charts_show_arrivals_billing_and_carriers()
    {
        var page = Render<Dashboard>();

        int Count(string label) => int.Parse(label[(label.LastIndexOf("total ", StringComparison.Ordinal) + 6)..]);
        string[] Labels(string chartId) => page.Find($"section[aria-labelledby='{chartId}-title']")
            .QuerySelectorAll(".col-slot").Select(s => s.GetAttribute("aria-label")!).ToArray();

        // Three active shipments arrive within the 8 weeks; the released one is left out.
        Assert.Equal(3, Labels("arrivals-chart").Sum(Count));
        // All four were invoiced this month: two paid (one since released), two not.
        var thisMonth = Labels("billing-chart")[^1];
        Assert.Contains("received " + 2000m.ToString("C0"), thisMonth);
        Assert.Contains("outstanding " + 2000m.ToString("C0"), thisMonth);

        Assert.Contains("Show as table", page.Markup);
        var carriers = page.FindAll(".bar-list li").Select(li => li.QuerySelector(".bar-name")!.TextContent).ToList();
        Assert.Equal(new[] { "Northwind Lines", "Bluewave Shipping" }, carriers);
    }
}
