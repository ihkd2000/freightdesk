namespace FreightDesk.Infra
{
    /// <summary>Abstraction over the outbound email provider so it can be faked in tests.</summary>
    public interface IMailSender
    {
        Task SendEmailAsync(string subject, string textBody, string htmlBody,
            string? to = null, string? from = null, CancellationToken cancellationToken = default);
    }
}
