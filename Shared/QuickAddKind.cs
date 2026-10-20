namespace FreightDesk.Shared
{
    public enum QuickAddKind
    {
        Client,
        Carrier,
        Port
    }

    public record QuickAddRequest(QuickAddKind Kind, string Name);
}
