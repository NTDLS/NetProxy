using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Accepts connections on one address and starts a proxied connection for each.
    /// </summary>
    internal class NpProxyListener
    {
        private TcpListener _listener;
        private Task? _acceptTask;

        internal NpProxy Proxy { get; private set; }

        public NpProxyListener(NpProxy proxy, TcpListener listener)
        {
            Proxy = proxy;
            _listener = listener;
        }

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
        /// Binds the port, so that a port that is in use is reported to whoever started the proxy, and starts accepting connections.
        /// </summary>
        public void Start()
        {
            //The accept loop takes connections off the queue as soon as they arrive, so a small backlog only makes connections that
            //  arrive together be refused. Values below 128 are raised to 128 (this setting was not applied at all before).
            var backlog = Proxy.Configuration.AcceptBacklogSize > 0 ? Math.Max(Proxy.Configuration.AcceptBacklogSize, NetProxy.Library.Constants.MinAcceptBacklogSize) : int.MaxValue;

            try
            {
                _listener.Start(backlog);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported
                && _listener.Server.AddressFamily == AddressFamily.InterNetworkV6 && _listener.Server.DualMode)
            {
                //IPv6 is installed but cannot be used (for example it is disabled on the network adapters). Listen on IPv4 only.
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"IPv6 is not available for proxy '{Proxy.Configuration.Name}', listening on IPv4 only.");

                try { _listener.Stop(); } catch { }

                _listener = new TcpListener(IPAddress.Any, Proxy.Configuration.ListenPort);
                _listener.Start(backlog);
            }

            _acceptTask = Task.Run(AcceptLoopAsync);
        }

        public void Stop()
        {
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
            var stopToken = Proxy.StopToken;

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
                    //A failure to accept one connection (for example a client that reset while it was waiting) must not stop the proxy.
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Proxy '{Proxy.Configuration.Name}' failed to accept a connection: {ex.Message}");
                    await Task.Delay(100, CancellationToken.None);
                    continue;
                }

                try
                {
                    client.NoDelay = true;

                    var connection = new NpProxyConnection(Proxy, client);

                    if (Proxy.TryAdmit(connection.ClientKey) == false)
                    {
                        connection.Refuse(); //At a connection limit.
                        continue;
                    }

                    Proxy.AddConnection(connection);
                    _ = connection.RunAsync(); //Never throws: the connection handles and logs its own failures.
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write($"Proxy '{Proxy.Configuration.Name}' failed to start a connection", ex);
                    try { client.Dispose(); } catch { }
                }
            }
        }
    }
}
