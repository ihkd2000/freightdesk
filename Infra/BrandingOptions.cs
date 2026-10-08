namespace FreightDesk.Infra
{
    /// <summary>Everything customer-facing that identifies the business. Bound from the "Branding" config section.</summary>
    public class BrandingOptions
    {
        public string CompanyName { get; set; } = "FreightDesk";
        public string Tagline { get; set; } = "Shipment tracking & billing";
        public string WebsiteUrl { get; set; } = string.Empty;
        public string TrackingUrl { get; set; } = string.Empty;
        public string SupportEmail { get; set; } = string.Empty;
        public string AccountingEmail { get; set; } = string.Empty;
    }
}
