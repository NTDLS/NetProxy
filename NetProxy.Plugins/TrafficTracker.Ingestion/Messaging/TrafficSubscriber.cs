using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NetProxy.TrafficTracker.Contracts;
using NetProxy.TrafficTracker.Ingestion.Commands;
using NetProxy.TrafficTracker.Ingestion.Database;
using NTDLS.CatMQ.Client;
using NTDLS.CatMQ.Shared;

namespace NetProxy.TrafficTracker.Ingestion.Messaging
{
    /// <summary>
    /// Subscribes to the traffic queue and turns every message into a command. It is the queue side of the service: it connects (and keeps
    /// trying until it can), makes the queue, receives the batches and says to the queue what became of each, and leaves the database to the
    /// command handlers.
    /// </summary>
    public sealed class TrafficSubscriber : BackgroundService
    {
        private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

        private readonly IngestionOptions _options;
        private readonly ICommandDispatcher _commands;
        private readonly DatabaseMigrator _migrator;
        private readonly ILogger<TrafficSubscriber> _logger;
        private readonly CMqClient _client = new();
        private CancellationToken _stopping;

        public TrafficSubscriber(IOptions<IngestionOptions> options, ICommandDispatcher commands, DatabaseMigrator migrator, ILogger<TrafficSubscriber> logger)
        {
            _options = options.Value;
            _commands = commands;
            _migrator = migrator;
            _logger = logger;

            _client.OnConnected += Client_OnConnected;
            _client.OnDisconnected += _ => _logger.LogWarning("The connection to the message queue was lost. It is connected again by itself.");
            _client.OnException += (_, queue, ex) => _logger.LogError(ex, "The message queue reported an error{Queue}.", queue == null ? "" : $" for {queue}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stopping = stoppingToken;

            //The database first: there is nothing to do with a message until it can be written. A database that is not there yet (the server
            //  is starting with the machine, for one) is waited for, and the messages wait in the queue.
            while (stoppingToken.IsCancellationRequested == false)
            {
                try
                {
                    await _migrator.MigrateAsync(stoppingToken);
                    _logger.LogInformation("The database is ready.");
                    break;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("The database is not ready ({Reason}). Trying again in {Seconds} seconds.", ex.GetBaseException().Message, _retryDelay.TotalSeconds);
                    await DelayAsync(stoppingToken);
                }
            }

            while (stoppingToken.IsCancellationRequested == false && _client.IsConnected == false)
            {
                try
                {
                    _client.Connect(_options.MqHost, _options.MqPort);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not connect to the message queue at {Host}:{Port} ({Reason}). Trying again in {Seconds} seconds.",
                        _options.MqHost, _options.MqPort, ex.GetBaseException().Message, _retryDelay.TotalSeconds);
                    await DelayAsync(stoppingToken);
                }
            }

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            try { _client.Disconnect(); } catch { }
        }

        private static async Task DelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_retryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Client_OnConnected(CMqClient client, bool isReconnect)
        {
            _logger.LogInformation(isReconnect ? "Reconnected to the message queue." : "Connected to the message queue at {Host}:{Port}.", _options.MqHost, _options.MqPort);

            //Not on the thread of the connection: the calls below wait for the server to answer.
            Task.Run(async () =>
            {
                try
                {
                    client.CreateQueue(TrafficQueue.CreateConfiguration(_options.Queue));

                    //The client subscribes again by itself after a reconnect.
                    if (isReconnect == false)
                    {
                        await client.SubscribeAsync(_options.Queue, OnBatchAsync);
                        _logger.LogInformation("Subscribed to the queue {Queue}.", _options.Queue);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not subscribe to the queue {Queue}.", _options.Queue);
                }
            });
        }

        /// <summary>
        /// What became of a batch: ingested (consumed), or not ingested for a reason that may pass (the database is down, so it is delivered
        /// again later), or never going to be (it cannot be read, so it goes to the dead-letter queue instead of being tried for ever).
        /// </summary>
        internal async Task<CMqConsumeResult> OnBatchAsync(CMqClient client, CMqReceivedMessage message)
        {
            WebRequestBatch? batch;

            try
            {
                batch = message.Deserialize<WebRequestBatch>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A message on {Queue} is not a batch of requests, and goes to the dead-letter queue.", message.QueueName);
                return CMqConsumeResult.DeadLetter();
            }

            if (batch == null)
            {
                _logger.LogError("A message on {Queue} is not a batch of requests, and goes to the dead-letter queue.", message.QueueName);
                return CMqConsumeResult.DeadLetter();
            }

            try
            {
                await _commands.SendAsync(new IngestWebRequests(batch.Source, batch.Requests), _stopping);

                _logger.LogDebug("Ingested a batch of {Count} requests from {Source}.", batch.Requests.Count, batch.Source);
                return CMqConsumeResult.Consumed();
            }
            catch (OperationCanceledException)
            {
                return CMqConsumeResult.NotConsumed();
            }
            catch (SqlException ex) when (IngestWebRequestsHandler.IsTransient(ex) || IsDataError(ex) == false)
            {
                //The database cannot be reached, or is busy (a deadlock that outlasted the retries is one): the batch is delivered again later,
                //  and never goes to the dead-letter queue for a reason that passes.
                _logger.LogWarning("A batch of {Count} requests could not be written ({Reason}) and is tried again in a while.", batch.Requests.Count, ex.Message);
                return CMqConsumeResult.Defer(TimeSpan.FromSeconds(30));
            }
            catch (SqlException ex)
            {
                //The data is what the database does not accept (too long for a column, a value that is not allowed): trying again will not help.
                _logger.LogError(ex, "A batch of {Count} requests from {Source} is not accepted by the database and goes to the dead-letter queue.", batch.Requests.Count, batch.Source);
                return CMqConsumeResult.DeadLetter();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A batch of {Count} requests could not be ingested and is tried again in a while.", batch.Requests.Count);
                return CMqConsumeResult.Defer(TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>
        /// What the database does not accept in the data itself (a value too long for a column, a null where there must not be one, a value that
        /// refers to nothing): trying again will not change it.
        /// </summary>
        internal static bool IsDataError(SqlException ex) => ex.Number is 515 or 547 or 8152 or 245 or 2628 or 220 or 8115;

        public override void Dispose()
        {
            _client.Dispose();
            base.Dispose();
        }
    }
}
