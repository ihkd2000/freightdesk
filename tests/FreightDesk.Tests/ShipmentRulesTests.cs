using FreightDesk.Data;
using FreightDesk.Infra;
using FreightDesk.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreightDesk.Tests;

public class ContainerNumberTests
{
    [Theory]
    [InlineData("CSQU3054383")] // ISO 6346 reference example
    [InlineData("MSCU1234566")] // the placeholder shown on the form
    [InlineData("csqu 305438-3")] // lower case, spaces and dashes are normalised
    public void Valid_numbers_pass(string number)
    {
        Assert.Null(ShipmentRules.ContainerNumberError(number));
    }

    [Theory]
    [InlineData("CSQU3054384")] // wrong check digit
    [InlineData("CSQA3054383")] // category letter must be U, J or Z
    [InlineData("CSQU305438")] // too short
    [InlineData("C5QU3054383")] // digit in the owner code
    public void Invalid_numbers_fail(string number)
    {
        Assert.NotNull(ShipmentRules.ContainerNumberError(number));
    }

    [Fact]
    public void Wrong_check_digit_message_names_the_expected_digit()
    {
        Assert.Contains("should end in 3", ShipmentRules.ContainerNumberError("CSQU3054389"));
    }

    [Fact]
    public void Empty_number_is_left_to_the_required_check()
    {
        Assert.Null(ShipmentRules.ContainerNumberError(""));
    }
}

public class ShipmentRuleTests
{
    private static Shipment Valid() => new()
    {
        JobReferenceNumber = "JOB-1",
        BookingNumber = "BK-1",
        ContainerNumber = "CSQU3054383",
        CutOffDate = new DateTime(2026, 11, 1),
        Sailing = new DateTime(2026, 11, 3),
        Arrival = new DateTime(2026, 11, 20),
        Price = 1200m,
        CargoQuantity = 1
    };

    private static IEnumerable<string> Fields(Shipment s, bool checkFormat = true) =>
        ShipmentRules.Validate(s, checkFormat).Select(e => e.Field);

    [Fact]
    public void A_complete_shipment_has_no_errors()
    {
        Assert.Empty(ShipmentRules.Validate(Valid(), true));
    }

    [Fact]
    public void Same_day_cut_off_sailing_and_arrival_are_allowed()
    {
        var s = Valid();
        s.CutOffDate = s.Sailing = s.Arrival = new DateTime(2026, 11, 3, 9, 0, 0);
        Assert.Empty(ShipmentRules.Validate(s, true));
    }

    [Fact]
    public void Cut_off_after_sailing_is_rejected()
    {
        var s = Valid();
        s.CutOffDate = s.Sailing!.Value.AddDays(1);
        Assert.Equal(new[] { nameof(Shipment.CutOffDate) }, Fields(s));
    }

    [Fact]
    public void Arrival_before_sailing_is_rejected()
    {
        var s = Valid();
        s.Arrival = s.Sailing!.Value.AddDays(-1);
        Assert.Equal(new[] { nameof(Shipment.Arrival) }, Fields(s));
    }

    [Fact]
    public void Arrival_before_cut_off_is_rejected_when_there_is_no_sailing_date()
    {
        var s = Valid();
        s.Sailing = null;
        s.Arrival = s.CutOffDate!.Value.AddDays(-1);
        Assert.Equal(new[] { nameof(Shipment.Arrival) }, Fields(s));
    }

    [Fact]
    public void Negative_price_zero_quantity_and_zero_invoice_are_rejected()
    {
        var s = Valid();
        s.Price = -1;
        s.CargoQuantity = 0;
        s.InvoiceNo = 0;
        Assert.Equal(
            new[] { nameof(Shipment.Price), nameof(Shipment.CargoQuantity), nameof(Shipment.InvoiceNo) },
            Fields(s));
    }

    [Fact]
    public void Container_format_is_only_checked_when_asked()
    {
        var s = Valid();
        s.ContainerNumber = "OLD-REF-7";
        Assert.Contains(nameof(Shipment.ContainerNumber), Fields(s, checkFormat: true));
        Assert.Empty(Fields(s, checkFormat: false));
    }
}

public sealed class DuplicateShipmentTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;

    public DuplicateShipmentTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Ports.Add(new Port { Id = 1, Name = "New York" });
        _db.Carriers.Add(new Carrier { Id = 1, Name = "Maersk" });
        _db.Clients.Add(new Client { Id = 1, Name = "ABC Shipping", Email = "abc@example.com" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Shipment Add(string container, string booking, int deleted = 0)
    {
        var s = new Shipment
        {
            Owner = "owner",
            JobReferenceNumber = "JOB",
            BookingNumber = booking,
            ContainerNumber = container,
            CarrierId = 1,
            PortId = 1,
            ClientId = 1,
            Deleted = deleted
        };
        _db.Shipments.Add(s);
        _db.SaveChanges();
        return s;
    }

    private static Shipment Candidate(string container, string booking) =>
        new() { ContainerNumber = container, BookingNumber = booking };

    [Fact]
    public async Task Same_container_and_booking_is_a_duplicate_ignoring_case_and_spacing()
    {
        var existing = Add("CSQU3054383", "BK-1");
        Assert.Equal(existing.Id, await ShipmentRules.FindDuplicateAsync(_db, Candidate("csqu 3054383", " bk-1 ")));
    }

    [Fact]
    public async Task Same_container_on_another_booking_is_not_a_duplicate()
    {
        Add("CSQU3054383", "BK-1");
        Assert.Null(await ShipmentRules.FindDuplicateAsync(_db, Candidate("CSQU3054383", "BK-2")));
    }

    [Fact]
    public async Task Deleted_shipments_are_ignored()
    {
        Add("CSQU3054383", "BK-1", deleted: 1);
        Assert.Null(await ShipmentRules.FindDuplicateAsync(_db, Candidate("CSQU3054383", "BK-1")));
    }

    [Fact]
    public async Task A_shipment_is_not_a_duplicate_of_itself()
    {
        var existing = Add("CSQU3054383", "BK-1");
        Assert.Null(await ShipmentRules.FindDuplicateAsync(_db, existing));
    }

    [Fact]
    public async Task Next_invoice_number_starts_at_one_and_follows_the_highest_used()
    {
        Assert.Equal(1, await ShipmentRules.NextInvoiceNumberAsync(_db));

        Add("CSQU3054383", "BK-1").InvoiceNo = 41;
        Add("MSCU1234566", "BK-2", deleted: 1).InvoiceNo = 57; // deleted shipments keep their number
        Add("TCLU1234568", "BK-3");
        _db.SaveChanges();

        Assert.Equal(58, await ShipmentRules.NextInvoiceNumberAsync(_db));
    }
}
