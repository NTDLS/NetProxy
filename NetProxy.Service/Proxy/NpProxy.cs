using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

namespace NetProxy.Service.Proxy
{
    public class NpProxy
    {
        /// <summary>
        /// An endpoint that could not be connected to is tried last for this long, so that connections are not each made to
        /// wait for it to time out. It is still tried if no other endpoint works.
        /// </summary>
        private static readonly TimeSpan _failureBackoff = TimeSpan.FromSeconds(10);

        private readonly object _lifecycleLock = new();
        private List<NpProxyListener> _listeners = [];
        private CancellationTokenSource? _stopSource;
        private MemoryCache? _stickySessions;
        private long _roundRobinCounter;
        private int _isRunning;

        public NpProxyConfiguration Configuration { get; private set; }
        public NpProxyStatistics Statistics { get; } = new();
        public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

        /// <summary>
        /// Statistics for each endpoint, by endpoint id.
        /// </summary>
        internal ConcurrentDictionary<Guid, NpProxyEndpointStatistics> EndpointStatistics { get; } = new();

        internal ConcurrentDictionary<Guid, NpProxyConnection> ActiveConnections { get; } = new();

        /// <summary>
        /// The certificate that the listener presents to clients, when it terminates TLS.
        /// </summary>
        internal X509Certificate2? Certificate { get; private set; }

        /// <summary>
        /// What the listener does with the traffic while the proxy is running. This is the configured traffic type, except that an
        /// Https listener without a certificate (the way HTTPS was configured before TLS could be terminated) passes the
        /// encrypted traffic through untouched, which is Raw.
        /// </summary>
        internal TrafficType EffectiveTrafficType { get; private set; }

        internal bool TerminatesTls => EffectiveTrafficType == TrafficType.Https;

        /// <summary>
        /// Cancelled when the proxy is stopped.
        /// </summary>
        internal CancellationToken StopToken => _stopSource?.Token ?? new CancellationToken(true);

        public NpProxy(NpProxyConfiguration configuration)
        {
            Configuration = configuration;
            EffectiveTrafficType = configuration.TrafficType;
        }

        public bool Start() => Start(out _);

        /// <summary>
        /// Starts listening. Returns false, with the reason, if it could not (for example the port is in use).
        /// </summary>
        public bool Start(out string? error)
        {
            lock (_lifecycleLock)
            {
                error = null;

                if (IsRunning)
                {
                    return true;
                }

                var started = new List<NpProxyListener>();

                try
                {
                    if (Configuration.Endpoints.Collection.Any(o => o.Enabled) == false)
                    {
                        throw new InvalidOperationException("The proxy has no enabled endpoints.");
                    }

                    LoadCertificate();

                    _stopSource = new CancellationTokenSource();
                    _stickySessions = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100_000 });
                    _roundRobinCounter = 0;

                    var endpointIds = Configuration.Endpoints.Collection.Select(o => o.Id).ToHashSet();
                    foreach (var staleId in EndpointStatistics.Keys.Where(o => endpointIds.Contains(o) == false))
                    {
                        EndpointStatistics.TryRemove(staleId, out _);
                    }
                    foreach (var endpointId in endpointIds)
                    {
                        EndpointStatistics.TryAdd(endpointId, new NpProxyEndpointStatistics());
                    }

                    foreach (var tcpListener in NpNetwork.CreateListeners(Configuration))
                    {
                        var listener = new NpProxyListener(this, tcpListener);
                        listener.Start();
                        started.Add(listener);
                    }

                    if (started.Count == 0)
                    {
                        throw new InvalidOperationException("The proxy has no enabled bindings.");
                    }

                    _listeners = started;
                    Volatile.Write(ref _isRunning, 1);

                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Proxy '{Configuration.Name}' is listening on {string.Join(", ", started.Select(o => o.Description))} ({EffectiveTrafficType}).");

                    return true;
                }
                catch (Exception ex)
                {
                    foreach (var listener in started)
                    {
                        listener.Stop();
                    }

                    ReleaseResources();

                    error = ex.Message;
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Failed to start proxy '{Configuration.Name}': {ex.Message}");
                    return false;
                }
            }
        }

        public void Stop()
        {
            List<NpProxyListener> listeners;
            CancellationTokenSource? stopSource;

            lock (_lifecycleLock)
            {
                if (IsRunning == false)
                {
                    return;
                }

                Volatile.Write(ref _isRunning, 0);
                listeners = _listeners;
                _listeners = [];
                stopSource = _stopSource;
            }

            stopSource?.Cancel();

            foreach (var listener in listeners)
            {
                listener.Stop();
            }

            var connections = ActiveConnections.Values.ToList();
            foreach (var connection in connections)
            {
                connection.Abort();
            }

            //The connections end as soon as they are aborted. Give them a moment to finish with their sockets.
            Task.WhenAll(connections.Select(o => o.Completion)).Wait(TimeSpan.FromSeconds(5));

            lock (_lifecycleLock)
            {
                ReleaseResources();
            }

            Singletons.Logging.Write(NpLogging.Severity.Information, $"Proxy '{Configuration.Name}' has stopped.");
        }

        private void LoadCertificate()
        {
            EffectiveTrafficType = Configuration.TrafficType;

            if (Configuration.TrafficType != TrafficType.Https)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Configuration.TlsCertificatePath))
            {
                //How HTTPS was configured before TLS could be terminated: the encrypted traffic is passed through.
                EffectiveTrafficType = TrafficType.Raw;
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"Proxy '{Configuration.Name}' is set to HTTPS but has no certificate, so the encrypted traffic is passed through untouched"
                    + " (header rules do not apply). Choose a certificate (PFX) to terminate TLS at the proxy.");
                return;
            }

            try
            {
                Certificate = X509CertificateLoader.LoadPkcs12FromFile(Configuration.TlsCertificatePath,
                    string.IsNullOrEmpty(Configuration.TlsCertificatePassword) ? null : Configuration.TlsCertificatePassword);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"The TLS certificate '{Configuration.TlsCertificatePath}' could not be loaded: {ex.GetBaseException().Message}", ex);
            }
        }

        private void ReleaseResources()
        {
            Certificate?.Dispose();
            Certificate = null;

            _stickySessions?.Dispose();
            _stickySessions = null;

            _stopSource?.Dispose();
            _stopSource = null;
        }

        internal void AddConnection(NpProxyConnection connection)
            => ActiveConnections[connection.Id] = connection;

        internal void RemoveConnection(NpProxyConnection connection)
            => ActiveConnections.TryRemove(connection.Id, out _);

        private bool IsFailing(NpEndpoint endpoint)
            => EndpointStatistics.TryGetValue(endpoint.Id, out var statistics)
                && statistics.LastFailure is DateTime lastFailure
                && DateTime.UtcNow - lastFailure < _failureBackoff;

        /// <summary>
        /// The enabled endpoints in the order that a new connection should try them, for the connection pattern of the proxy:
        ///   Fail-over: always in the configured order, so the first endpoint is used for as long as it works.
        ///   Round-robin: starting with the next endpoint in turn.
        ///   Balanced: starting with the endpoint that has the fewest connections.
        /// Endpoints that failed a moment ago are moved to the end, and with sticky sessions the endpoint that the client was
        /// last sent to is moved to the front (as long as it is not failing).
        /// </summary>
        internal List<NpEndpoint> GetEndpointAttemptOrder(string clientKey)
        {
            var endpoints = Configuration.Endpoints.Collection.Where(o => o.Enabled).ToList();

            if (endpoints.Count == 0)
            {
                return endpoints;
            }

            List<NpEndpoint> ordered;

            switch (Configuration.Endpoints.ConnectionPattern)
            {
                case ConnectionPattern.RoundRobbin:
                    {
                        int start = (int)((ulong)Interlocked.Increment(ref _roundRobinCounter) % (ulong)endpoints.Count);
                        ordered = [.. endpoints.Skip(start), .. endpoints.Take(start)];
                    }
                    break;

                case ConnectionPattern.Balanced:
                    ordered = [.. endpoints.OrderBy(o => EndpointStatistics.TryGetValue(o.Id, out var s) ? s.CurrentConnections : 0)];
                    break;

                default:
                    ordered = endpoints;
                    break;
            }

            ordered = [.. ordered.OrderBy(o => IsFailing(o) ? 1 : 0)];

            if (Configuration.UseStickySessions && _stickySessions != null
                && _stickySessions.TryGetValue(clientKey, out NpStickySession? session) && session != null)
            {
                var sticky = ordered.FirstOrDefault(o => o.Id == session.EndpointId)
                    ?? ordered.FirstOrDefault(o => o.Address == session.Address && o.Port == session.Port);

                if (sticky != null && IsFailing(sticky) == false)
                {
                    ordered.Remove(sticky);
                    ordered.Insert(0, sticky);
                }
            }

            return ordered;
        }

        /// <summary>
        /// Remembers the endpoint that a client was sent to, for as long as the sticky session cache expiration since it was last used.
        /// </summary>
        internal void RememberStickySession(string clientKey, NpEndpoint endpoint)
        {
            if (Configuration.UseStickySessions == false)
            {
                return;
            }

            var expiration = Configuration.StickySessionCacheExpiration > 0
                ? Configuration.StickySessionCacheExpiration : Constants.DefaultStickySessionExpiration;

            _stickySessions?.Set(clientKey, new NpStickySession(endpoint.Id, endpoint.Address, endpoint.Port), new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromSeconds(expiration),
                Size = 1
            });
        }
    }
}
