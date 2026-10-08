using FreightDesk.Data;
using FreightDesk.Infra;
using FreightDesk.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FreightDesk.Tests;

public sealed class FakeMailSender : IMailSender
{
    public List<(string To, string Subject, string Html)> Sent { get; } = new();
    public Func<string, bool>? FailFor { get; set; }

    public Task SendEmailAsync(string subject, string textBody, string htmlBody,
        string? to = null, string? from = null, CancellationToken cancellationToken = default)
    {
        if (to != null && FailFor?.Invoke(to) == true)
        {
            throw new HttpRequestException("simulated mail failure");
        }

        Sent.Add((to ?? string.Empty, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed class ArrivalEmailTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeMailSender _mail = new();
    private readonly ServiceProvider _services;

    public ArrivalEmailTests()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IMailSender>(_mail);
        services.AddScoped<EmailDispatcher>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Destinations.Add(new Destination { Id = 1, destination_name = "New York" });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private ArrivalEmailBGService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ArrivalNotifications:DaysBefore"] = "10" })
            .Build();
        return new ArrivalEmailBGService(_services, NullLogger<ArrivalEmailBGService>.Instance, config,
            Options.Create(new BrandingOptions { CompanyName = "Test Freight" }));
    }

    private int AddShipper(string name, string? email)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shipper = new Shipping { Name = name, Email = email };
        db.Shippings.Add(shipper);
        db.SaveChanges();
        return shipper.Id;
    }

    private int AddContainer(string number, DateTime? arrival, int shipperId, int deleted = 0, int? emailed = 0)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var container = new ContainerTR
        {
            Owner = "owner",
            JobReferenceNumber = "JOB-" + number,
            BookingNumber = "BK-" + number,
            ContainerNumber = number,
            DestinationId = 1,
            ShippingId = shipperId,
            Arrival = arrival,
            Deleted = deleted,
            Emailed = emailed
        };
        db.Containers.Add(container);
        db.SaveChanges();
        return container.Id;
    }

    private int? EmailedFlag(int containerId)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Containers.Single(c => c.Id == containerId).Emailed;
    }

    [Fact]
    public async Task Each_email_contains_only_its_own_container_details()
    {
        var shipper = AddShipper("A", "a@example.com");
        AddContainer("AAA1111111", DateTime.Now.AddDays(3), shipper);
        AddContainer("BBB2222222", DateTime.Now.AddDays(5), shipper);

        await CreateService().ProcessAsync(CancellationToken.None);

        Assert.Equal(2, _mail.Sent.Count);
        var first = Assert.Single(_mail.Sent, m => m.Html.Contains("AAA1111111"));
        var second = Assert.Single(_mail.Sent, m => m.Html.Contains("BBB2222222"));
        Assert.DoesNotContain("BBB2222222", first.Html);
        Assert.DoesNotContain("AAA1111111", second.Html);
        Assert.DoesNotContain("{ContainerNumber}", first.Html);
    }

    [Fact]
    public async Task Ineligible_containers_are_skipped()
    {
        var shipper = AddShipper("A", "a@example.com");
        var noEmail = AddShipper("NoEmail", "");

        AddContainer("DELETED0001", DateTime.Now.AddDays(3), shipper, deleted: 1);
        AddContainer("PAST0000001", DateTime.Now.AddDays(-5), shipper);
        AddContainer("FAR00000001", DateTime.Now.AddDays(30), shipper);
        AddContainer("DONE0000001", DateTime.Now.AddDays(3), shipper, emailed: 1);
        AddContainer("NOARRIVAL01", null, shipper);
        AddContainer("NOADDRESS01", DateTime.Now.AddDays(3), noEmail);

        await CreateService().ProcessAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Containers_are_marked_emailed_and_not_sent_twice()
    {
        var shipper = AddShipper("A", "a@example.com");
        var id = AddContainer("AAA1111111", DateTime.Now.AddDays(2), shipper);
        var service = CreateService();

        await service.ProcessAsync(CancellationToken.None);
        await service.ProcessAsync(CancellationToken.None);

        Assert.Single(_mail.Sent);
        Assert.Equal(1, EmailedFlag(id));
    }

    [Fact]
    public async Task A_failed_send_does_not_block_others_and_is_retried_later()
    {
        var good = AddShipper("Good", "good@example.com");
        var bad = AddShipper("Bad", "bad@example.com");
        var goodId = AddContainer("GOOD0000001", DateTime.Now.AddDays(2), good);
        var badId = AddContainer("BAD00000001", DateTime.Now.AddDays(2), bad);

        _mail.FailFor = to => to == "bad@example.com";
        var service = CreateService();
        await service.ProcessAsync(CancellationToken.None);

        Assert.Single(_mail.Sent);
        Assert.Equal(1, EmailedFlag(goodId));
        Assert.Equal(0, EmailedFlag(badId));

        _mail.FailFor = null;
        await service.ProcessAsync(CancellationToken.None);

        Assert.Equal(2, _mail.Sent.Count);
        Assert.Equal(1, EmailedFlag(badId));
    }
}

public class EmailTemplateTests
{
    [Fact]
    public void Arrival_notice_uses_configured_branding_and_encodes_values()
    {
        var brand = new BrandingOptions { CompanyName = "Acme <Freight>", AccountingEmail = "billing@acme.test" };
        var shipment = new ContainerTR { ContainerNumber = "<b>X1</b>", BookingNumber = "BK1", Arrival = new DateTime(2026, 11, 2) };

        var email = EmailTemplates.ArrivalNotice(brand, shipment, 10, new DateTime(2026, 10, 25));

        Assert.Contains("Acme &lt;Freight&gt;", email.Html);
        Assert.Contains("billing@acme.test", email.Html);
        Assert.Contains("&lt;b&gt;X1&lt;/b&gt;", email.Html);
        Assert.DoesNotContain("<b>X1</b>", email.Html);
    }

    [Theory]
    [InlineData(false, false, false, ShipmentStatus.Planned)]
    [InlineData(true, false, false, ShipmentStatus.AwaitingPayment)]
    [InlineData(true, true, false, ShipmentStatus.Paid)]
    [InlineData(true, true, true, ShipmentStatus.Released)]
    public void Status_is_derived_from_dates(bool arrival, bool paid, bool released, ShipmentStatus expected)
    {
        var s = new ContainerTR
        {
            Arrival = arrival ? DateTime.Today : null,
            PaymentReceivedDate = paid ? DateTime.Today : null,
            ReleaseDate = released ? DateTime.Today : null
        };

        Assert.Equal(expected, s.GetStatus());
    }
}
