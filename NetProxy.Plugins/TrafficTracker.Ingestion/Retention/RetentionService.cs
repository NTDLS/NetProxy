using Microsoft.Extensions.Options;
using NetProxy.TrafficTracker.Ingestion.Commands;

namespace NetProxy.TrafficTracker.Ingestion.Retention
{
    /// <summary>
    /// Removes the requests that are older than Ingestion:RetentionDays, once an hour. With 0 days nothing is ever removed and this does nothing.
    /// </summary>
    public sealed class RetentionService(IOptions<IngestionOptions> options, ICommandDispatcher commands, ILogger<RetentionService> logger) : BackgroundService
    {
        private static readonly TimeSpan _interval = TimeSpan.FromHours(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (options.Value.RetentionDays <= 0)
            {
                return;
            }

            logger.LogInformation("Requests older than {Days} days are removed.", options.Value.RetentionDays);

            //A first wait, so that the database is made (and the queue is being worked on) before there is anything to remove.
            using var timer = new PeriodicTimer(_interval);

            try
            {
                do
                {
                    try
                    {
                        await commands.SendAsync(new PurgeOldRequests(DateTime.UtcNow.AddDays(-options.Value.RetentionDays)), stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning("The old requests could not be removed ({Reason}). It is tried again in an hour.", ex.GetBaseException().Message);
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
