using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using NetProxy.Service.Plugins;
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
        private List<NpUdpListener> _udpListeners = [];
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

        internal ConcurrentDictionary<Guid, INpActiveConnection> ActiveConnections { get; } = new();

        /// <summary>
        /// The certificates that the listener presents to clients, when it terminates TLS. They are reloaded when their files change.
        /// </summary>
        private NpCertificateStore? _certificates;

        /// <summary>
        /// The certificate of the proxy, when it terminates TLS.
        /// </summary>
        internal X509Certificate2? Certificate => _certificates?.Default;

        /// <summary>
        /// The (normalized) host names that this proxy answers to. Empty for a proxy that is the default for its port.
        /// </summary>
        internal IReadOnlyCollection<string> HostPatterns { get; }

        /// <summary>
        /// True for a proxy without host names: it gets the requests that no other proxy on its port claims.
        /// </summary>
        internal bool IsDefaultForPort => NpHostRouter.IsDefault(Configuration);

        /// <summary>
        /// The certificate to present to a client that asks for a host name: the one of the most specific host name that matches
        /// and has its own, otherwise the certificate of the proxy. Null if there is none.
        /// </summary>
        internal X509Certificate2? GetCertificate(string? host) => _certificates?.Get(host);

        /// <summary>
        /// What the listener does with the traffic while the proxy is running. This is the configured traffic type, except that an
        /// Https listener without a certificate (the way HTTPS was configured before TLS could be terminated) passes the
        /// encrypted traffic through untouched, which is Tcp.
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
            HostPatterns = NpHostRouter.Patterns(configuration);

            //A proxy that replaces another (it was edited) or comes back with the service continues the numbers of the one with its id.
            Singletons.Statistics.Restore(this);
        }

        /// <summary>
        /// The proxy, as the plug-ins see it.
        /// </summary>
        internal NpProxyInfo Info => new(Configuration.Id, Configuration.Name, Configuration.ListenPort, Configuration.TrafficType switch
        {
            TrafficType.Http => NpTrafficKind.Http,
            TrafficType.Https => NpTrafficKind.Https,
            TrafficType.Udp => NpTrafficKind.Udp,
            _ => NpTrafficKind.Tcp
        });

        /// <summary>
        /// The plug-ins that are loaded and that this proxy has opted in to.
        /// </summary>
        internal NpPluginSet Plugins => Singletons.Plugins.ForProxy(Configuration);

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
                var startedUdp = new List<NpUdpListener>();
                var plugins = Plugins;

                try
                {
                    if (Configuration.Endpoints.Collection.Any(o => o.Enabled) == false)
                    {
                        throw new InvalidOperationException("The proxy has no enabled endpoints.");
                    }

                    var verdict = plugins.ProxyStartingAsync(Info).AsTask().GetAwaiter().GetResult();
                    if (verdict.Allowed == false)
                    {
                        throw new InvalidOperationException($"{verdict.Reason}");
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

                    if (Configuration.TrafficType == TrafficType.Udp)
                    {
                        //A UDP port belongs to one proxy. The sockets are bound here, so a port in use is reported to whoever started it.
                        startedUdp = NpUdpListener.StartAll(this);
                    }
                    else
                    {
                        foreach (var tcpListener in NpNetwork.CreateListeners(Configuration))
                        {
                            //Joins the listener of the port when another proxy already has it, if they can share it.
                            started.Add(NpProxyListener.Attach(this, tcpListener));
                        }
                    }

                    if (started.Count == 0 && startedUdp.Count == 0)
                    {
                        throw new InvalidOperationException("The proxy has no enabled bindings.");
                    }

                    _listeners = started;
                    _udpListeners = startedUdp;
                    Volatile.Write(ref _isRunning, 1);

                    var listeningOn = started.Select(o => o.Description).Concat(startedUdp.Select(o => o.Description)).ToList();

                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Proxy '{Configuration.Name}' is listening on {string.Join(", ", listeningOn)} ({EffectiveTrafficType}).");

                    plugins.ProxyStartedAsync(Info, listeningOn).AsTask().GetAwaiter().GetResult();

                    return true;
                }
                catch (Exception ex)
                {
                    foreach (var listener in started)
                    {
                        listener.Detach(this);
                    }

                    foreach (var listener in startedUdp)
                    {
                        listener.Stop();
                    }

                    ReleaseResources();

                    error = ex.Message;
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Failed to start proxy '{Configuration.Name}': {ex.Message}");

                    plugins.ProxyStartFailedAsync(Info, ex.Message).AsTask().GetAwaiter().GetResult();
                    return false;
                }
            }
        }

        public void Stop()
        {
            List<NpProxyListener> listeners;
            List<NpUdpListener> udpListeners;
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
                udpListeners = _udpListeners;
                _udpListeners = [];
                stopSource = _stopSource;
            }

            var plugins = Plugins;
            plugins.ProxyStoppingAsync(Info).AsTask().GetAwaiter().GetResult();

            stopSource?.Cancel();

            foreach (var listener in udpListeners)
            {
                listener.Stop();
            }

            //The port stays open for the other proxies that share it.
            foreach (var listener in listeners)
            {
                listener.Detach(this);
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

            plugins.ProxyStoppedAsync(Info).AsTask().GetAwaiter().GetResult();
        }

        private void LoadCertificate()
        {
            EffectiveTrafficType = Configuration.TrafficType;

            if (Configuration.TrafficType != TrafficType.Https)
            {
                return;
            }

            var hostsWithCertificate = Configuration.HostNames
                .Where(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificate) == false).ToList();

            if (string.IsNullOrWhiteSpace(Configuration.TlsCertificate) && hostsWithCertificate.Count == 0)
            {
                //How HTTPS was configured before TLS could be terminated: the encrypted traffic is passed through.
                EffectiveTrafficType = TrafficType.Tcp;
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"Proxy '{Configuration.Name}' is set to HTTPS but has no certificate, so the encrypted traffic is passed through untouched"
                    + " (header rules do not apply). Choose a certificate (PFX) to terminate TLS at the proxy.");
                return;
            }

            //Throws, with the reason, if a certificate is not set up or cannot be loaded. Renewed certificates are picked up while the proxy runs.
            _certificates = NpCertificateStore.Create(Configuration);
        }

        private void ReleaseResources()
        {
            _certificates = null;

            _stickySessions?.Dispose();
            _stickySessions = null;

            _stopSource?.Dispose();
            _stopSource = null;
        }

        private readonly object _admissionLock = new();
        private readonly Dictionary<string, int> _clientConnectionCounts = new();
        private int _admittedConnections;
        private long _refusedConnections;
        private long _lastRefusedLogTicks;

        /// <summary>
        /// Counts a new connection against the limits of the proxy. Returns false when a limit is reached, in which case the caller
        /// closes the connection and does not call <see cref="ReleaseClient"/>.
        /// </summary>
        internal bool TryAdmit(string clientKey)
        {
            var maxConnections = Configuration.MaxConnections;
            var maxPerClient = Configuration.MaxConnectionsPerClient;
            string limit;

            lock (_admissionLock)
            {
                _clientConnectionCounts.TryGetValue(clientKey, out var clientCount);

                if (maxConnections > 0 && _admittedConnections >= maxConnections)
                {
                    limit = $"the limit of {maxConnections} connections";
                }
                else if (maxPerClient > 0 && clientCount >= maxPerClient)
                {
                    limit = $"the limit of {maxPerClient} connections per client";
                }
                else
                {
                    _admittedConnections++;
                    _clientConnectionCounts[clientKey] = clientCount + 1;
                    return true;
                }
            }

            //Say so now and then, not for every connection that is refused: that is what a flood would do to the log.
            var refused = Interlocked.Increment(ref _refusedConnections);
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastRefusedLogTicks);
            if (now - last >= TimeSpan.FromMinutes(1).Ticks && Interlocked.CompareExchange(ref _lastRefusedLogTicks, now, last) == last)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"Proxy '{Configuration.Name}' refused a connection from {clientKey} because it is at {limit}"
                    + $" ({refused} refused since it started).");
            }

            return false;
        }

        internal void ReleaseClient(string clientKey)
        {
            lock (_admissionLock)
            {
                _admittedConnections = Math.Max(0, _admittedConnections - 1);

                if (_clientConnectionCounts.TryGetValue(clientKey, out var count))
                {
                    if (count <= 1)
                    {
                        _clientConnectionCounts.Remove(clientKey);
                    }
                    else
                    {
                        _clientConnectionCounts[clientKey] = count - 1;
                    }
                }
            }
        }

        internal void AddConnection(INpActiveConnection connection)
            => ActiveConnections[connection.Id] = connection;

        internal void RemoveConnection(INpActiveConnection connection)
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
