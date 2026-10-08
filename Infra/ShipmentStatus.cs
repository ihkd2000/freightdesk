using FreightDesk.Models;

namespace FreightDesk.Infra
{
    public enum ShipmentStatus
    {
        Planned,
        AwaitingPayment,
        Paid,
        Released
    }

    public static class ShipmentStatusExtensions
    {
        public static ShipmentStatus GetStatus(this Shipment shipment)
        {
            if (shipment.ReleaseDate.HasValue) return ShipmentStatus.Released;
            if (shipment.PaymentReceivedDate.HasValue) return ShipmentStatus.Paid;
            if (shipment.Arrival.HasValue) return ShipmentStatus.AwaitingPayment;
            return ShipmentStatus.Planned;
        }

        public static string Label(this ShipmentStatus status) => status switch
        {
            ShipmentStatus.AwaitingPayment => "Awaiting payment",
            ShipmentStatus.Paid => "Paid",
            ShipmentStatus.Released => "Released",
            _ => "Planned"
        };

        public static string CssClass(this ShipmentStatus status) => status switch
        {
            ShipmentStatus.AwaitingPayment => "status-awaiting",
            ShipmentStatus.Paid => "status-paid",
            ShipmentStatus.Released => "status-released",
            _ => "status-planned"
        };
    }
}
