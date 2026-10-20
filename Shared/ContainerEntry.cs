namespace FreightDesk.Shared
{
    /// <summary>An extra container row on the New shipment form; each one becomes its own shipment.</summary>
    public class ContainerEntry
    {
        public string Number { get; set; } = "";
        public string? Error { get; set; }
    }
}
