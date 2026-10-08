using FreightDesk.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreightDesk.Infra
{
    /// <summary>
    /// Emails each client once when a shipment is about to arrive.
    /// Settings (optional): ArrivalNotifications:DaysBefore (default 10), ArrivalNotifications:PollMinutes (default 15).
    /// </summary>
    public class ArrivalEmailBGService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ArrivalEmailBGService> _logger;
        private readonly BrandingOptions _brand;
        private readonly int _daysBefore;
        private readonly TimeSpan _pollInterval;

        public ArrivalEmailBGService(IServiceProvider serviceProvider, ILogger<ArrivalEmailBGService> logger,
            IConfiguration configuration, IOptions<BrandingOptions> brand)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _brand = brand.Value;
            _daysBefore = Math.Max(1, configuration.GetValue("ArrivalNotifications:DaysBefore", 10));
            _pollInterval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("ArrivalNotifications:PollMinutes", 15)));
        }

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
                    _logger.LogError(ex, "Error in ArrivalEmailBGService.");
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
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();

            var now = DateTime.Now;
            var limit = now.AddDays(_daysBefore);

            // Only shipments that are still upcoming, inside the window, not deleted and not yet emailed.
            var shipments = await dbContext.Shipments
                .Include(c => c.Port)
                .Include(c => c.Client)
                .Where(c => c.Deleted != 1
                            && (c.Emailed ?? 0) == 0
                            && c.Arrival != null
                            && c.Arrival >= now.Date
                            && c.Arrival <= limit)
                .ToListAsync(stoppingToken);

            foreach (var shipment in shipments)
            {
                var recipient = shipment.Client?.Email;
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    _logger.LogWarning("Shipment {ContainerNumber} has no client email; skipping.", shipment.ContainerNumber);
                    continue;
                }

                try
                {
                    var email = EmailTemplates.ArrivalNotice(_brand, shipment, _daysBefore, now);

                    var result = await dispatcher.SendAsync(EmailKinds.ArrivalNotice, recipient,
                        email.Subject, email.Text, email.Html, shipment.Id, stoppingToken);
                    if (!result.Success)
                    {
                        continue; // logged by the dispatcher; retried on the next pass
                    }

                    // Save right after each successful send so a later failure cannot cause a re-send.
                    shipment.Emailed = 1;
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Sent arrival notice for {ContainerNumber}.", shipment.ContainerNumber);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process arrival notice for {ContainerNumber}.", shipment.ContainerNumber);
                }
            }
        }
    }
}
