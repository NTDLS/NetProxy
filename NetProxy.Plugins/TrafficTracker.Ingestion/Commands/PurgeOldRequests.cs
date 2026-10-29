using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace NetProxy.Plugins.TrafficTracker.Ingestion.Commands
{
    /// <summary>
    /// Removes the requests that are older than a time. The hosts, clients, user agents and the rest stay: they are small, and it is useful to know
    /// when each was first seen.
    /// </summary>
    public sealed record PurgeOldRequests(DateTime OlderThanUtc) : ICommand
    {
        /// <summary>How many were removed. Set by the handler.</summary>
        public long Removed { get; set; }
    }

    internal sealed class PurgeOldRequestsHandler(IOptions<IngestionOptions> options, ILogger<PurgeOldRequestsHandler> logger)
        : ICommandHandler<PurgeOldRequests>
    {
        public async Task HandleAsync(PurgeOldRequests command, CancellationToken cancellationToken)
        {
            var batchSize = Math.Clamp(options.Value.PurgeBatchSize, 100, 1_000_000);

            await using var connection = new SqlConnection(options.Value.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            long total = 0;
            int removed;

            //A batch at a time, so that the table is not locked for long and the log of the database does not have to hold it all.
            do
            {
                removed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "traffic.PurgeWebRequests",
                    new { OlderThanUtc = command.OlderThanUtc, BatchSize = batchSize },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 300,
                    cancellationToken: cancellationToken));

                total += removed;
            }
            while (removed >= batchSize && cancellationToken.IsCancellationRequested == false);

            command.Removed = total;

            if (total > 0)
            {
                logger.LogInformation("Removed {Count:n0} requests from before {Cutoff:u}.", total, command.OlderThanUtc);
            }
        }
    }
}
