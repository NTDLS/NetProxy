using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NetProxy.TrafficTracker.Contracts;
using NetProxy.TrafficTracker.Ingestion.Database;
using System.Data;

namespace NetProxy.TrafficTracker.Ingestion.Commands
{
    /// <summary>
    /// Writes the requests of a batch to the database, normalized.
    /// </summary>
    public sealed record IngestWebRequests(string Source, IReadOnlyList<WebRequestRecord> Requests) : ICommand;

    internal sealed class IngestWebRequestsHandler(IOptions<IngestionOptions> options, ILogger<IngestWebRequestsHandler> logger)
        : ICommandHandler<IngestWebRequests>
    {
        private const int Attempts = 5;

        //One batch at a time in this service: the database takes them one at a time anyway (see 002_SerializeIngestion.sql), and the
        //  batches that wait here do not hold a connection while they do.
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task HandleAsync(IngestWebRequests command, CancellationToken cancellationToken)
        {
            if (command.Requests.Count == 0)
            {
                return;
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                await IngestWithRetriesAsync(command, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task IngestWithRetriesAsync(IngestWebRequests command, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await IngestAsync(command, cancellationToken);
                    return;
                }
                catch (SqlException ex) when (attempt < Attempts && IsTransient(ex))
                {
                    //Two ingestions that meet over the same new value, or a deadlock between them: one of them gets another go.
                    logger.LogWarning("The ingestion of {Count} requests met a conflict ({Number}) and is tried again.", command.Requests.Count, ex.Number);
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt + Random.Shared.Next(0, 100)), cancellationToken);
                }
            }
        }

        private async Task IngestAsync(IngestWebRequests command, CancellationToken cancellationToken)
        {
            using var table = WebRequestTable.Build(command.Source, command.Requests);

            await using var connection = new SqlConnection(options.Value.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var inserted = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "traffic.IngestWebRequests",
                new { Requests = table.AsTableValuedParameter(WebRequestTable.TypeName) },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120,
                cancellationToken: cancellationToken));

            logger.LogDebug("Ingested {Inserted} of {Count} requests from {Source}.", inserted, command.Requests.Count, command.Source);
        }

        /// <summary>
        /// 2601 and 2627 are a value that someone else added first, 1205 is a deadlock victim.
        /// </summary>
        internal static bool IsTransient(SqlException ex) => ex.Number is 2601 or 2627 or 1205;
    }
}
