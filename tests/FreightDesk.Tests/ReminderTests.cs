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

public sealed class PaymentReminderTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeMailSender _mail = new();
    private readonly ServiceProvider _services;

    public PaymentReminderTests()
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

    private PaymentReminderBGService CreateService() => new(
        _services,
        NullLogger<PaymentReminderBGService>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentReminders:DaysBefore:0"] = "5",
            ["PaymentReminders:DaysBefore:1"] = "2"
        }).Build(),
        Options.Create(new BrandingOptions { CompanyName = "Test Freight" }));

    private int AddShipment(string number, DateTime? arrival, string? clientEmail = "client@example.com",
        DateTime? paid = null, DateTime? released = null, int deleted = 0)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = new Shipping { Name = "Client " + number, Email = clientEmail };
        db.Shippings.Add(client);
        db.SaveChanges();

        var shipment = new ContainerTR
        {
            Owner = "owner",
            JobReferenceNumber = "JOB-" + number,
            BookingNumber = "BK-" + number,
            ContainerNumber = number,
            DestinationId = 1,
            ShippingId = client.Id,
            Arrival = arrival,
            PaymentReceivedDate = paid,
            ReleaseDate = released,
            Deleted = deleted,
            Price = 1200m
        };
        db.Containers.Add(shipment);
        db.SaveChanges();
        return shipment.Id;
    }

    private void SetArrival(int id, DateTime arrival)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Containers.Single(c => c.Id == id).Arrival = arrival;
        db.SaveChanges();
    }

    private int RemindersSent(int id)
    {
        using var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Containers.Single(c => c.Id == id).PaymentRemindersSent;
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(6, 0)]
    [InlineData(5, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 2)]
    public void Stages_due_depend_on_days_left(int daysLeft, int expected)
    {
        Assert.Equal(expected, PaymentReminderBGService.StagesDue(new[] { 5, 2 }, daysLeft));
    }

    [Fact]
    public async Task Nothing_is_sent_before_the_first_stage()
    {
        AddShipment("EARLY000001", DateTime.Now.AddDays(8));

        await CreateService().ProcessAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task One_reminder_per_stage_and_no_repeats()
    {
        var id = AddShipment("STAGE000001", DateTime.Now.AddDays(4));
        var service = CreateService();

        await service.ProcessAsync(CancellationToken.None);
        await service.ProcessAsync(CancellationToken.None);
        Assert.Single(_mail.Sent);
        Assert.Equal(1, RemindersSent(id));

        SetArrival(id, DateTime.Now.AddDays(1));
        await service.ProcessAsync(CancellationToken.None);
        await service.ProcessAsync(CancellationToken.None);

        Assert.Equal(2, _mail.Sent.Count);
        Assert.Equal(2, RemindersSent(id));
        Assert.All(_mail.Sent, m => Assert.Contains("STAGE000001", m.Html));
    }

    [Fact]
    public async Task Missed_stages_send_a_single_email()
    {
        var id = AddShipment("LATE0000001", DateTime.Now.AddDays(1));

        await CreateService().ProcessAsync(CancellationToken.None);

        Assert.Single(_mail.Sent);
        Assert.Equal(2, RemindersSent(id));
    }

    [Fact]
    public async Task Paid_released_deleted_and_past_shipments_are_skipped()
    {
        AddShipment("PAID0000001", DateTime.Now.AddDays(3), paid: DateTime.Today);
        AddShipment("REL00000001", DateTime.Now.AddDays(3), released: DateTime.Today);
        AddShipment("DEL00000001", DateTime.Now.AddDays(3), deleted: 1);
        AddShipment("PAST0000001", DateTime.Now.AddDays(-3));
        AddShipment("NOARRIVAL01", null);
        AddShipment("NOEMAIL0001", DateTime.Now.AddDays(3), clientEmail: "");

        await CreateService().ProcessAsync(CancellationToken.None);

        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Failed_send_is_logged_and_retried_on_the_next_pass()
    {
        var id = AddShipment("RETRY000001", DateTime.Now.AddDays(3));
        var service = CreateService();

        _mail.FailFor = _ => true;
        await service.ProcessAsync(CancellationToken.None);
        Assert.Empty(_mail.Sent);
        Assert.Equal(0, RemindersSent(id));

        _mail.FailFor = null;
        await service.ProcessAsync(CancellationToken.None);
        Assert.Single(_mail.Sent);
        Assert.Equal(1, RemindersSent(id));

        using var scope = _services.CreateScope();
        var logs = scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailLogs.OrderBy(l => l.Id).ToList();
        Assert.Equal(2, logs.Count);
        Assert.False(logs[0].Success);
        Assert.Equal("simulated mail failure", logs[0].Error);
        Assert.True(logs[1].Success);
        Assert.All(logs, l => Assert.Equal(EmailKinds.PaymentReminder, l.Kind));
        Assert.All(logs, l => Assert.Equal(id, l.ContainerId));
    }
}

public sealed class EmailDispatcherTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeMailSender _mail = new();
    private readonly ServiceProvider _services;

    public EmailDispatcherTests()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IMailSender>(_mail);
        services.AddScoped<EmailDispatcher>();
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Success_and_failure_are_both_logged_with_their_bodies()
    {
        using var scope = _services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ok = await dispatcher.SendAsync(EmailKinds.ArrivalNotice, "a@example.com", "Hello", "text", "<p>html</p>", 7);
        _mail.FailFor = _ => true;
        var bad = await dispatcher.SendAsync(EmailKinds.PaymentReminder, "b@example.com", "Oops", "t2", "<p>h2</p>");

        Assert.True(ok.Success);
        Assert.False(bad.Success);

        var logs = db.EmailLogs.OrderBy(l => l.Id).ToList();
        Assert.Equal(2, logs.Count);
        Assert.True(logs[0].Success);
        Assert.Equal(7, logs[0].ContainerId);
        Assert.Equal("<p>html</p>", logs[0].HtmlBody);
        Assert.False(logs[1].Success);
        Assert.Equal("simulated mail failure", logs[1].Error);
    }

    [Fact]
    public async Task Resend_sends_the_same_content_again_and_logs_it()
    {
        using var scope = _services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _mail.FailFor = _ => true;
        await dispatcher.SendAsync(EmailKinds.ShipmentRegistered, "c@example.com", "Registered", "text", "<p>body</p>", 3);
        var failedId = db.EmailLogs.Single().Id;

        _mail.FailFor = null;
        var result = await dispatcher.ResendAsync(failedId);

        Assert.True(result.Success);
        var sent = Assert.Single(_mail.Sent);
        Assert.Equal("c@example.com", sent.To);
        Assert.Equal("Registered", sent.Subject);
        Assert.Equal("<p>body</p>", sent.Html);

        var newest = db.EmailLogs.OrderByDescending(l => l.Id).First();
        Assert.Equal(EmailKinds.Resend, newest.Kind);
        Assert.True(newest.Success);
        Assert.Equal(3, newest.ContainerId);
    }

    [Fact]
    public async Task Resend_of_unknown_log_reports_an_error()
    {
        using var scope = _services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();

        var result = await dispatcher.ResendAsync(999);

        Assert.False(result.Success);
        Assert.Empty(_mail.Sent);
    }
}

public class AttentionRuleTests
{
    private static readonly DateTime Today = new(2026, 10, 7);

    private static ContainerTR Shipment(DateTime? arrival, DateTime? paid = null, DateTime? released = null,
        string? email = "client@example.com") => new()
    {
        Arrival = arrival,
        PaymentReceivedDate = paid,
        ReleaseDate = released,
        Shipping = new Shipping { Name = "C", Email = email }
    };

    [Fact]
    public void Unpaid_after_arrival_is_overdue()
    {
        var reasons = AttentionRules.Evaluate(Shipment(Today.AddDays(-2)), Today, 10);
        Assert.Equal(new[] { AttentionReason.PaymentOverdue }, reasons);
    }

    [Fact]
    public void Unpaid_inside_the_window_is_flagged_but_outside_is_not()
    {
        Assert.Equal(new[] { AttentionReason.UnpaidArrivingSoon }, AttentionRules.Evaluate(Shipment(Today.AddDays(4)), Today, 10));
        Assert.Empty(AttentionRules.Evaluate(Shipment(Today.AddDays(30)), Today, 10));
        Assert.Empty(AttentionRules.Evaluate(Shipment(null), Today, 10));
    }

    [Fact]
    public void Paid_and_arrived_is_ready_to_release_until_it_is_released()
    {
        Assert.Equal(new[] { AttentionReason.ReadyToRelease }, AttentionRules.Evaluate(Shipment(Today, paid: Today.AddDays(-1)), Today, 10));
        Assert.Empty(AttentionRules.Evaluate(Shipment(Today, paid: Today.AddDays(-1), released: Today), Today, 10));
        Assert.Empty(AttentionRules.Evaluate(Shipment(Today.AddDays(3), paid: Today), Today, 10));
    }

    [Fact]
    public void Missing_client_email_is_flagged_for_open_shipments_only()
    {
        Assert.Contains(AttentionReason.MissingClientEmail, AttentionRules.Evaluate(Shipment(null, email: " "), Today, 10));
        Assert.Empty(AttentionRules.Evaluate(Shipment(null, released: Today, email: null), Today, 10));
    }
}
