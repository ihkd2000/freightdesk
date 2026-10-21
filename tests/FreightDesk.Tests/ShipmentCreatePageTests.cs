using System.Security.Claims;
using Bunit;
using FreightDesk.Data;
using FreightDesk.Infra;
using FreightDesk.Models;
using FreightDesk.Pages.Shipments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreightDesk.Tests;

/// <summary>Drives the New shipment page end to end: form, pickers, save, database and email.</summary>
public sealed class ShipmentCreatePageTests : BunitContext
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeMailSender _mail = new();

    public ShipmentCreatePageTests()
    {
        _connection.Open();
        Services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        Services.AddSingleton<IMailSender>(_mail);
        Services.AddScoped<EmailDispatcher>();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(Options.Create(new BrandingOptions { CompanyName = "Test Freight" }));

        var auth = AddAuthorization();
        auth.SetAuthorized("tester");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        JSInterop.Mode = JSRuntimeMode.Loose;

        using var db = NewDb();
        db.Database.EnsureCreated();
        db.Clients.AddRange(
            new Client { Id = 1, Name = "ABC Shipping", Email = "abc@example.com" },
            new Client { Id = 2, Name = "XYZ Shipping", Email = "xyz@example.com" });
        db.Carriers.Add(new Carrier { Id = 1, Name = "Northwind Lines" });
        db.Ports.Add(new Port { Id = 1, Name = "New York" });
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private List<Shipment> SavedShipments()
    {
        using var db = NewDb();
        return db.Shipments.OrderBy(s => s.Id).ToList();
    }

    private static void Pick(IRenderedComponent<ShipmentCreate> page, string pickerId, string search)
    {
        page.Find($"#{pickerId}").Focus();
        page.Find($"#{pickerId}").Input(search);
        page.Find($"#{pickerId}-list li").MouseDown();
    }

    private IRenderedComponent<ShipmentCreate> RenderFilled(string container = "CSQU3054383", string booking = "BK-1")
    {
        var page = Render<ShipmentCreate>();
        page.Find("#jobRef").Change("JOB-1");
        page.Find("#bookingNo").Change(booking);
        page.Find("#containerNo").Change(container);
        Pick(page, "client", "abc");
        Pick(page, "carrier", "north");
        Pick(page, "port", "york");
        page.Find("#arrival").Change(DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"));
        page.Find("#price").Change("1200");
        return page;
    }

    [Fact]
    public void Saving_stores_the_shipment_and_emails_the_client_once()
    {
        var page = RenderFilled(container: "csqu 305438-3");

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("saved and the client was emailed", page.Find(".alert").TextContent));
        var saved = Assert.Single(SavedShipments());
        Assert.Equal(("CSQU3054383", 1, 1, 1, "user-1", 1), (saved.ContainerNumber, saved.ClientId, saved.CarrierId, saved.PortId, saved.Owner, saved.InvoiceNo));
        Assert.Equal(DateTime.Today, saved.InvoiceDate);
        var email = Assert.Single(_mail.Sent);
        Assert.Equal(("abc@example.com", "Shipment registered: CSQU3054383"), (email.To, email.Subject));
    }

    [Fact]
    public void Unticking_notify_saves_without_emailing()
    {
        var page = RenderFilled();
        page.Find("#notifyClient").Change(false);
        Assert.Equal("Save shipment", page.Find("button[type=submit]").TextContent.Trim());

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("The client was not emailed", page.Find(".alert").TextContent));
        Assert.Single(SavedShipments());
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public void Several_containers_become_separate_shipments_with_one_email()
    {
        var page = RenderFilled();
        AddContainerButton(page).Click();
        page.Find("input[aria-label='Container 2']").Change("MSCU1234566");

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("2 shipments on booking BK-1 saved", page.Find(".alert").TextContent));
        var saved = SavedShipments();
        Assert.Equal(new[] { "CSQU3054383", "MSCU1234566" }, saved.Select(s => s.ContainerNumber));
        Assert.All(saved, s => Assert.Equal(("BK-1", 1, 1200m), (s.BookingNumber, s.ClientId, s.Price!.Value)));
        var email = Assert.Single(_mail.Sent);
        Assert.Equal("Shipment registered: 2 containers on booking BK-1", email.Subject);
    }

    [Fact]
    public void A_repeated_container_in_the_form_blocks_the_save()
    {
        var page = RenderFilled();
        AddContainerButton(page).Click();
        page.Find("input[aria-label='Container 2']").Change("csqu3054383");

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("This container is already listed.", page.Markup));
        Assert.Empty(SavedShipments());
    }

    [Fact]
    public void An_existing_container_and_booking_is_not_saved_twice()
    {
        RenderFilled().Find("form").Submit();
        WaitFor(() => SavedShipments().Count == 1);
        _mail.Sent.Clear();

        var page = RenderFilled();
        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("This shipment already exists.", page.Find(".alert").TextContent));
        Assert.Single(SavedShipments());
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public void Arrival_before_sailing_is_shown_and_nothing_is_saved()
    {
        var page = RenderFilled();
        page.Find("#sailing").Change(DateTime.Today.AddDays(40).ToString("yyyy-MM-dd"));

        page.Find("form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Arrival must be on or after the sailing date.", page.Markup));
        Assert.Contains("Please fix the highlighted fields.", page.Find(".alert").TextContent);
        Assert.Empty(SavedShipments());
    }

    [Fact]
    public void A_new_port_can_be_added_from_the_picker_and_is_selected()
    {
        var page = RenderFilled();
        page.Find("#port").Focus();
        page.Find("#port").Input("Rotterdam");
        page.Find("#port-list li.lookup-create").MouseDown();

        page.Find("#quickAddName").Input("Rotterdam");
        page.Find(".fd-modal button.btn-primary").Click();

        page.WaitForAssertion(() => Assert.Equal("Rotterdam", page.Find("#port").GetAttribute("value")));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(SavedShipments()));

        using var db = NewDb();
        var port = db.Ports.Single(p => p.Name == "Rotterdam");
        Assert.Equal(port.Id, SavedShipments()[0].PortId);
    }

    [Fact]
    public void The_summary_lists_the_upcoming_automatic_emails()
    {
        var page = RenderFilled();

        var summary = page.Find(".shipment-summary").TextContent;
        Assert.Contains("ABC Shipping", summary);
        Assert.Contains("abc@example.com", summary);
        Assert.Contains(DateTime.Today.AddDays(20).ToString("ddd d MMM"), summary); // arrival notice, 10 days before
        Assert.Contains(EmailKinds.PaymentReminder, summary);
    }

    private static AngleSharp.Dom.IElement AddContainerButton(IRenderedComponent<ShipmentCreate> page) =>
        page.FindAll("button").Single(b => b.TextContent.Contains("Add another container"));

    private static void WaitFor(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Condition was not met in time.");
            Thread.Sleep(20);
        }
    }
}
