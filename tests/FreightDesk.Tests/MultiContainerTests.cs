using FreightDesk.Infra;
using FreightDesk.Models;

namespace FreightDesk.Tests;

public class MultiContainerTests
{
    private static Shipment Booking() => new()
    {
        Id = 7,
        Owner = "owner",
        JobReferenceNumber = "JOB-1",
        BookingNumber = "BK-1",
        ContainerNumber = "CSQU3054383",
        CarrierId = 1,
        PortId = 2,
        ClientId = 3,
        Client = new Client { Id = 3, Name = "ABC Shipping" },
        Arrival = new DateTime(2026, 11, 20),
        Price = 1000m,
        Notes = "Fragile"
    };

    [Fact]
    public void Copy_keeps_booking_details_but_is_a_new_shipment_for_the_other_container()
    {
        var original = Booking();
        var copy = original.CopyForContainer("MSCU1234566");

        Assert.Equal(0, copy.Id);
        Assert.Equal("MSCU1234566", copy.ContainerNumber);
        Assert.Null(copy.Client); // only the foreign keys are copied, so EF does not re-insert the client
        Assert.Equal(
            (original.BookingNumber, original.JobReferenceNumber, original.CarrierId, original.PortId, original.ClientId, original.Arrival, original.Price, original.Notes),
            (copy.BookingNumber, copy.JobReferenceNumber, copy.CarrierId, copy.PortId, copy.ClientId, copy.Arrival, copy.Price, copy.Notes));
        Assert.Equal("CSQU3054383", original.ContainerNumber);
        Assert.Equal(7, original.Id);
    }

    [Fact]
    public void Registration_email_for_one_container_is_unchanged()
    {
        var email = EmailTemplates.ShipmentRegistered(new BrandingOptions { CompanyName = "Test Freight" }, "ABC",
            Booking(), null, new DateTime(2026, 11, 1));

        Assert.Equal("Shipment registered: CSQU3054383", email.Subject);
        Assert.Contains("Container number:</strong> CSQU3054383", email.Html);
        Assert.Contains((1000m).ToString("C"), email.Html);
    }

    [Fact]
    public void Registration_email_for_a_booking_lists_every_container_and_the_total()
    {
        var email = EmailTemplates.ShipmentRegistered(new BrandingOptions { CompanyName = "Test Freight" }, "ABC",
            Booking(), null, new DateTime(2026, 11, 1), new[] { "CSQU3054383", "MSCU1234566", "TCLU1234560" });

        Assert.Equal("Shipment registered: 3 containers on booking BK-1", email.Subject);
        Assert.Contains("Containers (3):</strong> CSQU3054383, MSCU1234566, TCLU1234560", email.Html);
        Assert.Contains((3000m).ToString("C"), email.Html);
        Assert.Contains("containers CSQU3054383, MSCU1234566, TCLU1234560", email.Text);
    }
}
