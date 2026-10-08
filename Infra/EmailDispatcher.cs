using FreightDesk.Data;
using FreightDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FreightDesk.Infra
{
    public static class EmailKinds
    {
        public const string ShipmentRegistered = "Shipment registered";
        public const string ArrivalNotice = "Arrival notice";
        public const string PaymentReminder = "Payment reminder";
        public const string Resend = "Resend";
    }

    public record SendResult(bool Success, string? Error);

    /// <summary>Sends an email through <see cref="IMailSender"/> and records the outcome in the email log.</summary>
    public class EmailDispatcher
    {
        private readonly AppDbContext _db;
        private readonly IMailSender _mailer;
        private readonly ILogger<EmailDispatcher> _logger;

        public EmailDispatcher(AppDbContext db, IMailSender mailer, ILogger<EmailDispatcher> logger)
        {
            _db = db;
            _mailer = mailer;
            _logger = logger;
        }

        public async Task<SendResult> SendAsync(string kind, string to, string subject, string textBody, string htmlBody,
            int? containerId = null, CancellationToken cancellationToken = default)
        {
            string? error = null;
            try
            {
                await _mailer.SendEmailAsync(subject, textBody, htmlBody, to, null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                error = Truncate(ex.Message, 500);
                _logger.LogError(ex, "Could not send '{Subject}' to {To}.", subject, to);
            }

            _db.EmailLogs.Add(new EmailLog
            {
                SentAtUtc = DateTime.UtcNow,
                ToAddress = Truncate(to, 200),
                Subject = Truncate(subject, 300),
                Kind = Truncate(kind, 40),
                Success = error is null,
                Error = error,
                TextBody = textBody,
                HtmlBody = htmlBody,
                ContainerId = containerId
            });

            // Always record the attempt, even if the host is shutting down.
            await _db.SaveChangesAsync(CancellationToken.None);

            return new SendResult(error is null, error);
        }

        /// <summary>Sends a logged email again, to the same address with the same content.</summary>
        public async Task<SendResult> ResendAsync(int logId, CancellationToken cancellationToken = default)
        {
            var original = await _db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == logId, cancellationToken);
            if (original is null)
            {
                return new SendResult(false, "That email is no longer in the log.");
            }

            return await SendAsync(EmailKinds.Resend, original.ToAddress, original.Subject,
                original.TextBody, original.HtmlBody, original.ContainerId, cancellationToken);
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
