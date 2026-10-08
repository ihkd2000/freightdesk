using FreightDesk.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreightDesk.Infra
{
    /// <summary>
    /// Reminds clients who have not paid as their container's arrival gets close.
    /// Settings (optional): PaymentReminders:DaysBefore (default [5, 2]), PaymentReminders:PollMinutes (default 30).
    /// One email per stage; if several stages were missed, a single email is sent.
    /// </summary>
    public class PaymentReminderBGService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentReminderBGService> _logger;
        private readonly BrandingOptions _brand;
        private readonly string? _bankAccount;
        private readonly int[] _offsets;
        private readonly TimeSpan _pollInterval;

        public PaymentReminderBGService(IServiceProvider serviceProvider, ILogger<PaymentReminderBGService> logger,
            IConfiguration configuration, IOptions<BrandingOptions> brand)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _brand = brand.Value;
            _bankAccount = configuration["Billing:BankAccount"];

            var configured = configuration.GetSection("PaymentReminders:DaysBefore").Get<int[]>();
            _offsets = (configured is { Length: > 0 } ? configured : new[] { 5, 2 })
                .Where(d => d >= 0).Distinct().OrderByDescending(d => d).ToArray();
            _pollInterval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("PaymentReminders:PollMinutes", 30)));
        }

        /// <summary>Number of reminder stages that are due when <paramref name="daysLeft"/> days remain.</summary>
        public static int StagesDue(IReadOnlyCollection<int> offsets, int daysLeft) =>
            offsets.Count(offset => daysLeft <= offset);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PaymentReminderBGService.");
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>One pass over the database; public so it can be tested without running the loop.</summary>
        public async Task ProcessAsync(CancellationToken stoppingToken)
        {
            if (_offsets.Length == 0) return;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();

            var now = DateTime.Now;
            var limit = now.Date.AddDays(_offsets[0] + 1);

            var shipments = await dbContext.Containers
                .Include(c => c.Shipping)
                .Where(c => c.Deleted != 1
                            && c.PaymentReceivedDate == null
                            && c.ReleaseDate == null
                            && c.Arrival != null
                            && c.Arrival >= now.Date
                            && c.Arrival < limit)
                .ToListAsync(stoppingToken);

            foreach (var shipment in shipments)
            {
                var daysLeft = (shipment.Arrival!.Value.Date - now.Date).Days;
                var stages = StagesDue(_offsets, daysLeft);
                if (stages <= shipment.PaymentRemindersSent)
                {
                    continue;
                }

                var recipient = shipment.Shipping?.Email;
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    _logger.LogWarning("Shipment {ContainerNumber} is unpaid but its client has no email.", shipment.ContainerNumber);
                    continue;
                }

                try
                {
                    var email = EmailTemplates.PaymentReminder(_brand, shipment, daysLeft, _bankAccount, now);

                    var result = await dispatcher.SendAsync(EmailKinds.PaymentReminder, recipient,
                        email.Subject, email.Text, email.Html, shipment.Id, stoppingToken);
                    if (!result.Success)
                    {
                        continue; // logged by the dispatcher; retried on the next pass
                    }

                    shipment.PaymentRemindersSent = stages;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Sent payment reminder for {ContainerNumber}.", shipment.ContainerNumber);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process payment reminder for {ContainerNumber}.", shipment.ContainerNumber);
                }
            }
        }
    }
}
