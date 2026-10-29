using NetProxy.PluginApi;

namespace NetProxy.TrafficTracker.Plugin
{
    /// <summary>
    /// Tracks the web traffic that passes through the proxies that use it: for every HTTP request the host, the client, the user agent, the
    /// path and more, with the status of the response, sent to a CatMQ queue for the ingestion service to put in a database.
    ///
    /// It only looks. The data is passed on untouched, a request is followed for as long as it is HTTP/1.x (HTTP/2 is compressed, and is
    /// not followed), and nothing here waits for the queue: the requests are put in a buffer that a background task sends from.
    /// </summary>
    [NpPlugin("Traffic Tracker", Version = "1.0", Author = "NetworkDLS",
        Description = "Tracks the web traffic of a proxy (host, client, user agent, path, status and more) and sends it to a CatMQ queue.")]
    public sealed class TrafficTrackerPlugin : NpPlugin
    {
        private TrackerSettings _settings = new();
        private TrafficPublisher? _publisher;
        private int _warnedHttp2;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            _settings = TrackerSettings.From(Host.Settings, out var problems);

            foreach (var problem in problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _publisher = new TrafficPublisher(_settings, (text, isWarning) => Host.Log(isWarning ? NpPluginLogLevel.Warning : NpPluginLogLevel.Information, text));
            _publisher.Start();

            Host.Log(NpPluginLogLevel.Information,
                $"Tracking web traffic to queue '{_settings.Queue}' on {_settings.MqHost}:{_settings.MqPort} as '{_settings.Source}'.");

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            _publisher?.Stop(TimeSpan.FromSeconds(3));
            _publisher?.Dispose();
            _publisher = null;
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var publisher = _publisher;
                var connection = data.Connection;

                if (publisher == null || IsTracked(connection) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning,
                            "A client uses HTTP/2, which is compressed and is not tracked. Untick Allow HTTP/2 on the proxy to have those requests tracked.");
                    }

                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(ConnectionTracker.StateKey, out var existing) == false)
                {
                    existing = new ConnectionTracker(connection, _settings, publisher.Enqueue);
                    connection.State[ConnectionTracker.StateKey] = existing;
                }

                ((ConnectionTracker)existing!).Feed(data.Direction, data.Data.Span);
            }
            catch (Exception ex)
            {
                //Tracking is never allowed to get in the way of the traffic.
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be tracked: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
        {
            try
            {
                if (connection.State.TryGetValue(ConnectionTracker.StateKey, out var existing) && existing is ConnectionTracker tracker)
                {
                    tracker.Finish();
                    connection.State.Remove(ConnectionTracker.StateKey);
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A connection could not be finished: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// HTTP and HTTPS proxies are followed, and TCP proxies only when the plug-in is set to (they can carry plain HTTP). UDP never is.
        /// </summary>
        private bool IsTracked(INpConnection connection) => connection.Proxy.Traffic switch
        {
            NpTrafficKind.Http or NpTrafficKind.Https => true,
            NpTrafficKind.Tcp => _settings.IncludeTcpProxies,
            _ => false
        };
    }
}
