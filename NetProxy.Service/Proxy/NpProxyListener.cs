using NetProxy.Library;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Accepts connections on one address and port and starts a proxied connection for each. The listener belongs to the port, not to
    /// a proxy: HTTP and HTTPS proxies can share it, and each connection goes to the proxy that answers to the host name the client
    /// asks for (see <see cref="NpHostRouter"/>). The port is closed when the last proxy leaves.
    /// </summary>
    internal class NpProxyListener
    {
        private static readonly object _registryLock = new();
        private static readonly Dictionary<string, NpProxyListener> _registry = new();

        private readonly string _key;
        private readonly CancellationTokenSource _stopSource = new();
        private TcpListener _listener;
        private Task? _acceptTask;
        private NpProxy[] _routes = [];

        private NpProxyListener(string key, TcpListener listener)
        {
            _key = key;
            _listener = listener;
        }

        /// <summary>
        /// The proxies that have this port.
        /// </summary>
        public IReadOnlyList<NpProxy> Routes => Volatile.Read(ref _routes);

        /// <summary>
        /// Cancelled when the port is closed.
        /// </summary>
        public CancellationToken StopToken => _stopSource.Token;

        /// <summary>
        /// What the listener is listening on, for display.
        /// </summary>
        public string Description
        {
            get
            {
                if (_listener.LocalEndpoint is IPEndPoint endPoint)
                {
                    var any = endPoint.Address.Equals(IPAddress.Any) || endPoint.Address.Equals(IPAddress.IPv6Any);
                    var family = _listener.Server.AddressFamily == AddressFamily.InterNetworkV6 && _listener.Server.DualMode
                        ? "IPv4 and IPv6" : (endPoint.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4");
                    return any ? $"port {endPoint.Port} ({family})" : NpNetwork.FormatHostPort(endPoint.Address.ToString(), endPoint.Port);
                }

                return $"{_listener.LocalEndpoint}";
            }
        }

        /// <summary>
        /// Puts a proxy on the port of the listener it asks for. The port is bound if no other proxy has it (which throws if it is in
        /// use by something else), otherwise the proxy joins the listener of the others, provided that they can share the port.
        /// </summary>
        public static NpProxyListener Attach(NpProxy proxy, TcpListener requested)
        {
            lock (_registryLock)
            {
                var key = GetKey(requested);

                if (_registry.TryGetValue(key, out var existing))
                {
                    var conflict = existing.FindConflict(proxy);
                    if (conflict != null)
                    {
                        throw new InvalidOperationException(conflict);
                    }

                    existing.AddRoute(proxy);
                    return existing;
                }

                var listener = new NpProxyListener(key, requested);
                listener.Start(proxy);
                listener.AddRoute(proxy);
                _registry[key] = listener;
                return listener;
            }
        }

        /// <summary>
        /// Takes a proxy off the port, and closes the port if it was the last one.
        /// </summary>
        public void Detach(NpProxy proxy)
        {
            lock (_registryLock)
            {
                Volatile.Write(ref _routes, _routes.Where(o => o != proxy).ToArray());

                if (_routes.Length > 0)
                {
                    return;
                }

                if (_registry.TryGetValue(_key, out var registered) && registered == this)
                {
                    _registry.Remove(_key);
                }

                //Closed before the lock is let go, so that a proxy that starts next can bind the port.
                Stop();
            }
        }

        private static string GetKey(TcpListener listener)
        {
            var endPoint = (IPEndPoint)listener.LocalEndpoint;
            return $"{endPoint.Address}|{endPoint.Port}|{(listener.Server.DualMode ? "dual" : "single")}";
        }

        private void AddRoute(NpProxy proxy)
            => Volatile.Write(ref _routes, [.. _routes, proxy]);

        /// <summary>
        /// Why the proxy cannot join the proxies that already have the port, or null if it can.
        /// </summary>
        private string? FindConflict(NpProxy proxy)
        {
            var current = _routes;
            var port = proxy.Configuration.ListenPort;

            if (current.Length == 0)
            {
                return null;
            }

            var other = current[0];
            var inUse = $"Port {port} is already in use by proxy '{other.Configuration.Name}'.";

            if (proxy.EffectiveTrafficType == TrafficType.Raw || current.Any(o => o.EffectiveTrafficType == TrafficType.Raw))
            {
                return $"{inUse} Only HTTP and HTTPS proxies can share a port.";
            }

            if (current.Any(o => o.TerminatesTls != proxy.TerminatesTls))
            {
                return $"{inUse} Proxies can only share a port if they are all HTTP or all HTTPS.";
            }

            foreach (var route in current)
            {
                if (route.HostPatterns.Count == 0 && proxy.HostPatterns.Count == 0)
                {
                    return $"{inUse} To share a port, give each proxy host names (only one can have none, and be the default).";
                }

                var duplicate = route.HostPatterns.Intersect(proxy.HostPatterns).FirstOrDefault();
                if (duplicate != null)
                {
                    return $"{inUse} Both answer to the host name '{duplicate}'.";
                }
            }

            return null;
        }

        /// <summary>
        /// Binds the port, so that a port that is in use is reported to whoever started the proxy, and starts accepting connections.
        /// </summary>
        private void Start(NpProxy proxy)
        {
            //The accept loop takes connections off the queue as soon as they arrive, so a small backlog only makes connections that
            //  arrive together be refused. Values below 128 are raised to 128 (this setting was not applied at all before).
            var backlog = proxy.Configuration.AcceptBacklogSize > 0 ? Math.Max(proxy.Configuration.AcceptBacklogSize, NetProxy.Library.Constants.MinAcceptBacklogSize) : int.MaxValue;

            try
            {
                _listener.Start(backlog);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported
                && _listener.Server.AddressFamily == AddressFamily.InterNetworkV6 && _listener.Server.DualMode)
            {
                //IPv6 is installed but cannot be used (for example it is disabled on the network adapters). Listen on IPv4 only.
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"IPv6 is not available for proxy '{proxy.Configuration.Name}', listening on IPv4 only.");

                try { _listener.Stop(); } catch { }

                _listener = new TcpListener(IPAddress.Any, proxy.Configuration.ListenPort);
                _listener.Start(backlog);
            }

            _acceptTask = Task.Run(AcceptLoopAsync);
        }

        private void Stop()
        {
            try { _stopSource.Cancel(); } catch (ObjectDisposedException) { }
            try { _listener.Stop(); } catch { }

            try
            {
                _acceptTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                //The accept loop ends with an exception when the listener is stopped. That is how it is meant to end.
            }
        }

        private async Task AcceptLoopAsync()
        {
            var stopToken = _stopSource.Token;

            while (stopToken.IsCancellationRequested == false)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(stopToken);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
                {
                    break; //The listener was stopped.
                }
                catch (SocketException ex) when (stopToken.IsCancellationRequested || ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //A failure to accept one connection (for example a client that reset while it was waiting) must not stop the port.
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Failed to accept a connection on {Description}: {ex.Message}");
                    await Task.Delay(100, CancellationToken.None);
                    continue;
                }

                try
                {
                    client.NoDelay = true;

                    var routes = Routes;
                    if (routes.Count == 0)
                    {
                        client.Dispose(); //The last proxy just left.
                        continue;
                    }

                    //With one proxy on the port every connection is its own, whatever host name it asks for. With several, the
                    //  connection works out which one it is for.
                    _ = new NpProxyConnection(this, client, routes.Count == 1 ? routes[0] : null).RunAsync(); //Never throws: the connection handles and logs its own failures.
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write($"Failed to start a connection on {Description}", ex);
                    try { client.Dispose(); } catch { }
                }
            }
        }
    }
}
