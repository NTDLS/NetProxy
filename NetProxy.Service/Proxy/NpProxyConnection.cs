using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using NetProxy.Service.Http;
using NetProxy.Service.Http.Http2;
using NetProxy.Service.Plugins;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// One client connection, and the connection to the endpoint that it is proxied to. Data is pumped both ways until both sides
    /// are finished. When the client connected over TLS (the listener terminates it) the connection to the endpoint can be
    /// plain or its own TLS session, depending on the endpoint.
    /// </summary>
    internal sealed class NpProxyConnection : INpConnection, INpActiveConnection
    {
        /// <summary>
        /// A TLS handshake must complete within this time, so that a client that connects and goes quiet does not hold the connection.
        /// </summary>
        private static readonly TimeSpan _handshakeTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long to wait on one endpoint to accept a connection before trying the next.
        /// </summary>
        private static readonly TimeSpan _endpointConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly NpProxyListener _listener;

        /// <summary>
        /// The proxy that the connection is for. When the port is shared by several proxies it is not known until the client says
        /// which host name it wants (the Host header, or the server name of the TLS handshake), so this is only used once it is known.
        /// </summary>
        private NpProxy _proxy = null!;

        private string ProxyName => _proxy?.Configuration.Name ?? "(not chosen yet)";

        private readonly TcpClient _client;
        private readonly CancellationTokenSource _abort;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _clientKey;
        private readonly string _clientDisplay;
        private long _lastActivityTicks = DateTime.UtcNow.Ticks;

        /// <summary>
        /// True when the client speaks HTTP/2 (negotiated with ALPN when the listener terminates TLS, or detected by its preface otherwise).
        /// </summary>
        private bool _clientIsHttp2;

        /// <summary>
        /// The first bytes of the client, when they had to be read to tell what it speaks. They are passed on before anything else.
        /// </summary>
        private byte[]? _initialClientBytes;

        /// <summary>
        /// True when the first bytes of the client were read to find its host name, which also told if it speaks HTTP/2.
        /// </summary>
        private bool _peeked;

        /// <summary>
        /// True once the connection is counted against the limits of its proxy.
        /// </summary>
        private bool _admitted;

        /// <summary>
        /// True once the plug-ins have been told that the connection is connecting, so they are told when it ends too.
        /// </summary>
        private bool _connecting;

        private NpPluginSet _plugins = NpPluginSet.Empty;
        private NpDisconnectReason _disconnectReason = NpDisconnectReason.Completed;
        private string? _hostName;
        private NpEndpointInfo? _endpointInfo;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _state = new();

        private Stream? _clientStream;
        private TcpClient? _endpointClient;
        private Stream? _endpointStream;
        private NpProxyEndpointStatistics? _endpointStatistics;
        private long _bytesIn;
        private long _bytesOut;

        private readonly string _listenAddress = string.Empty;
        private readonly int _listenPort;
        private readonly int _clientPort;
        private string _endpointAddress = string.Empty;
        private int _endpointPort;

        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The address of the client, as used to count its connections.
        /// </summary>
        public string ClientKey => _clientKey;

        public DateTime StartDateTime { get; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);

        /// <summary>
        /// Completes when the connection is over and its sockets are closed.
        /// </summary>
        public Task Completion => _completion.Task;

        /// <summary>
        /// The proxy is null when the port is shared, in which case the connection works out which proxy it is for.
        /// </summary>
        public NpProxyConnection(NpProxyListener listener, TcpClient client, NpProxy? proxy)
        {
            _listener = listener;
            _proxy = proxy!;
            _client = client;
            _abort = CancellationTokenSource.CreateLinkedTokenSource(proxy?.StopToken ?? listener.StopToken);

            if (client.Client.RemoteEndPoint is IPEndPoint remote)
            {
                //Clients that connected over IPv4 to a dual-stack listener are shown (and remembered) by their IPv4 address.
                _clientKey = NpNetwork.Unmap(remote.Address).ToString();
                _clientDisplay = NpNetwork.FormatHostPort(_clientKey, remote.Port);
                _clientPort = remote.Port;
            }
            else
            {
                _clientKey = $"{client.Client.RemoteEndPoint}";
                _clientDisplay = _clientKey;
            }

            if (client.Client.LocalEndPoint is IPEndPoint local)
            {
                _listenAddress = NpNetwork.Unmap(local.Address).ToString();
                _listenPort = local.Port;
            }
        }

        /// <summary>
        /// A snapshot of the connection for the connection list of the console.
        /// </summary>
        public NpConnectionGridItem GetGridItem()
        {
            var clientTls = (_clientStream as SslStream)?.SslProtocol;
            var endpointTls = (_endpointStream as SslStream)?.SslProtocol;
            var trafficType = _proxy.EffectiveTrafficType;

            return new NpConnectionGridItem
            {
                Id = Id,
                StartDateTime = StartDateTime,
                LastActivityDateTime = LastActivityDateTime,
                SourceAddress = _clientKey,
                SourceHostName = NpHostNames.Get(_clientKey),
                SourcePort = _clientPort,
                ListenAddress = _listenAddress,
                ListenPort = _listenPort,
                DestinationAddress = _endpointAddress,
                DestinationHostName = NpHostNames.Get(_endpointAddress),
                DestinationPort = _endpointPort,
                Protocol = trafficType.ToString(),
                Version = trafficType is TrafficType.Http or TrafficType.Https ? (_clientIsHttp2 ? "HTTP/2" : "HTTP/1.x") : string.Empty,
                ClientTls = FormatTls(clientTls),
                EndpointTls = FormatTls(endpointTls),
                IsConnectedToEndpoint = _endpointStream != null,
                BytesIn = (ulong)Interlocked.Read(ref _bytesIn),
                BytesOut = (ulong)Interlocked.Read(ref _bytesOut)
            };
        }

        private static string FormatTls(SslProtocols? protocol) => protocol switch
        {
            null or SslProtocols.None => string.Empty,
            SslProtocols.Tls13 => "TLS 1.3",
            SslProtocols.Tls12 => "TLS 1.2",
            _ => protocol.ToString() ?? string.Empty
        };

        /// <summary>
        /// Ends the connection. Whatever is waiting on one of its sockets is released.
        /// </summary>
        public void Abort()
        {
            try { _abort.Cancel(); } catch (ObjectDisposedException) { }
            try { _client.Client?.Close(0); } catch { }
            try { _endpointClient?.Client?.Close(0); } catch { }
        }

        /// <summary>
        /// Counts the connection against the limits of its proxy and starts watching it. Returns false (and the connection is closed
        /// by the caller) when the proxy is at one of its limits, or has been stopped.
        /// </summary>
        private async Task<bool> AdmitAsync()
        {
            //From here on the plug-ins that the proxy uses hear about the connection.
            _plugins = _proxy.Plugins;

            if (_proxy.TryAdmit(_clientKey) == false)
            {
                _disconnectReason = NpDisconnectReason.Denied;
                await _plugins.ConnectionRefusedAsync(_proxy.Info, _clientKey, "The proxy is at one of its connection limits.");
                return false; //At a connection limit.
            }

            _admitted = true;
            _proxy.AddConnection(this);
            _proxy.Statistics.ConnectionOpened();

            if (_proxy.IsRunning == false)
            {
                _disconnectReason = NpDisconnectReason.Aborted;
                return false; //Stopped while the connection was being set up. It was added too late to be aborted by the stop.
            }

            _ = IdleWatchAsync();

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Proxy '{_proxy.Configuration.Name}' accepted {_clientDisplay} ({Id}).");
            return true;
        }

        private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        /// <summary>
        /// Runs the connection from start to finish. It does not throw: whatever goes wrong is logged and ends the connection.
        /// </summary>
        public async Task RunAsync()
        {
            try
            {
                //With one proxy on the port the limits apply before any work is done for the connection.
                if (_proxy != null && await AdmitAsync() == false)
                {
                    return;
                }

                var terminatesTls = _proxy?.TerminatesTls ?? _listener.Routes.FirstOrDefault()?.TerminatesTls ?? false;

                if (terminatesTls)
                {
                    //Chooses the proxy too, by the server name that the client asks for, when that is not known yet.
                    if (await TerminateClientTlsAsync() == false)
                    {
                        return;
                    }
                }
                else
                {
                    _clientStream = _client.GetStream();

                    if (_proxy == null && await PeekAndRouteAsync() == false)
                    {
                        return;
                    }
                }

                if (_admitted == false && await AdmitAsync() == false)
                {
                    return;
                }

                if (_clientStream is SslStream)
                {
                    await _plugins.ClientTlsEstablishedAsync(this);
                }

                _connecting = true;
                var verdict = await _plugins.ConnectingAsync(this);
                if (verdict.Allowed == false)
                {
                    _disconnectReason = NpDisconnectReason.Denied;
                    Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} was not allowed. {verdict.Reason}");
                    return;
                }

                if (await DetectClientHttp2Async() == false)
                {
                    return;
                }

                if (await ConnectToEndpointAsync() == false)
                {
                    if (_disconnectReason == NpDisconnectReason.Completed)
                    {
                        _disconnectReason = NpDisconnectReason.EndpointUnavailable;
                    }
                    return;
                }

                await _plugins.ConnectedAsync(this);

                await Task.WhenAll(
                    PumpAsync(_clientStream!, _endpointStream!, direction: ConnectionDirection.Inbound, _initialClientBytes),
                    PumpAsync(_endpointStream!, _clientStream!, direction: ConnectionDirection.Outbound));
            }
            catch (OperationCanceledException)
            {
                //The proxy is stopping, or the connection was aborted.
                MarkEnded(NpDisconnectReason.Aborted);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or AuthenticationException)
            {
                MarkEnded(NpDisconnectReason.Aborted);
                Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} ended: {ex.GetBaseException().Message}");
            }
            catch (Exception ex)
            {
                MarkEnded(NpDisconnectReason.Error);
                Singletons.Logging.Write($"Connection {Id} from {_clientDisplay} failed", ex);
                await _plugins.ErrorAsync(new NpPluginError($"Connection {Id} from {_clientDisplay} failed: {ex.GetBaseException().Message}", ex, _proxy?.Info, this));
            }
            finally
            {
                if (_connecting)
                {
                    //The sockets are still open for the plug-ins to look at, and are closed once they are done.
                    await _plugins.DisconnectingAsync(this, _disconnectReason);
                }

                CloseSockets();

                if (_connecting)
                {
                    var info = new NpDisconnectInfo(_disconnectReason, DateTime.UtcNow - StartDateTime,
                        Interlocked.Read(ref _bytesIn), Interlocked.Read(ref _bytesOut));
                    await _plugins.DisconnectedAsync(this, info);
                }

                Finish();
            }
        }

        /// <summary>
        /// Remembers why the connection ended, unless something more telling has already said.
        /// </summary>
        private void MarkEnded(NpDisconnectReason reason)
        {
            if (_disconnectReason == NpDisconnectReason.Completed)
            {
                _disconnectReason = reason;
            }
        }

        //---------------------------------------------------------------- INpConnection (what plug-ins see)

        NpProxyInfo INpConnection.Proxy => _proxy.Info;
        DateTime INpConnection.StartedUtc => StartDateTime;
        string INpConnection.ClientAddress => _clientKey;
        int INpConnection.ClientPort => _clientPort;
        string INpConnection.ListenAddress => _listenAddress;
        int INpConnection.ListenPort => _listenPort;
        string? INpConnection.HostName => _hostName;
        string? INpConnection.ClientTls => NullIfEmpty(FormatTls((_clientStream as SslStream)?.SslProtocol));
        string? INpConnection.EndpointTls => NullIfEmpty(FormatTls((_endpointStream as SslStream)?.SslProtocol));
        bool INpConnection.IsHttp2 => _clientIsHttp2;
        NpEndpointInfo? INpConnection.Endpoint => _endpointInfo;
        long INpConnection.BytesFromClient => Interlocked.Read(ref _bytesIn);
        long INpConnection.BytesFromEndpoint => Interlocked.Read(ref _bytesOut);
        IDictionary<string, object?> INpConnection.State => _state;

        void INpConnection.Close(string reason)
        {
            _disconnectReason = NpDisconnectReason.ClosedByPlugin;
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} was closed by a plugin: {reason}");
            Abort();
        }

        private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

        /// <summary>
        /// When the listener terminates TLS, completes the handshake with the client. Returns false (after logging why) if it fails.
        /// </summary>
        private async Task<bool> TerminateClientTlsAsync()
        {
            var ssl = new SslStream(_client.GetStream(), false);
            _clientStream = ssl;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
            timeout.CancelAfter(_handshakeTimeout);

            try
            {
                //The server name that the client asks for (SNI) picks the proxy, when the port is shared, and the certificate.
                await ssl.AuthenticateAsServerAsync(
                    (_, hello, _, _) => ValueTask.FromResult(SelectTlsOptions(hello.ServerName)), null, timeout.Token);

                _clientIsHttp2 = ssl.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2;
                return true;
            }
            catch (AuthenticationException ex)
            {
                //Usually a client that does not trust the certificate and hung up.
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"TLS handshake with {_clientDisplay} failed on proxy '{ProxyName}': {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                if (_abort.IsCancellationRequested == false)
                {
                    //Browsers routinely open connections that they drop before the handshake is done.
                    Singletons.Logging.Write(NpLogging.Severity.Verbose,
                        $"TLS handshake with {_clientDisplay} on proxy '{ProxyName}' did not complete: {ex.GetBaseException().Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Chooses the proxy for a TLS client (if the port is shared) and what it is offered: the certificate for the server name
        /// it asked for, and HTTP/2 if the proxy allows it. Throws if there is no proxy or certificate for it, which fails the handshake.
        /// </summary>
        private SslServerAuthenticationOptions SelectTlsOptions(string? serverName)
        {
            var proxy = _proxy ?? NpHostRouter.Match(_listener.Routes, serverName);

            if (proxy == null)
            {
                throw new AuthenticationException($"No proxy on this port answers to the host name '{serverName}'.");
            }

            var certificate = proxy.GetCertificate(serverName)
                ?? throw new AuthenticationException($"Proxy '{proxy.Configuration.Name}' has no certificate for the host name '{serverName}'.");

            _proxy = proxy;
            _hostName = NpHostMatcher.Normalize(serverName);

            return new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                ApplicationProtocols = proxy.Configuration.AllowHttp2
                    ? [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11]
                    : [SslApplicationProtocol.Http11],
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.None, //Let the operating system choose.
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
            };
        }

        /// <summary>
        /// For a port that is shared by several plain (not TLS) HTTP proxies: reads the start of the request to find the Host header,
        /// which picks the proxy. What was read is kept, to be passed on first. A client that speaks HTTP/2 without TLS has no Host
        /// header to read (it is compressed), so it goes to the default proxy of the port. A request for a host name that no proxy
        /// answers to (and there is no default) gets a 404. Returns false if the connection should end.
        /// </summary>
        private async Task<bool> PeekAndRouteAsync()
        {
            var preface = Http2StreamParser.ClientPrefaceBytes;
            var held = new MemoryStream();
            var buffer = new byte[4096];
            string? host = null;
            var isHttp2 = false;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
            timeout.CancelAfter(_handshakeTimeout);

            try
            {
                while (held.Length < NpHostRouter.MaxHeaderBytes)
                {
                    var read = await _clientStream!.ReadAsync(buffer.AsMemory(), timeout.Token);
                    if (read <= 0)
                    {
                        break;
                    }

                    Touch();
                    held.Write(buffer, 0, read);

                    var data = held.GetBuffer().AsSpan(0, (int)held.Length);
                    var compared = Math.Min(data.Length, preface.Length);

                    if (data[..compared].SequenceEqual(preface.AsSpan(0, compared)))
                    {
                        if (compared == preface.Length)
                        {
                            isHttp2 = true;
                            break;
                        }

                        continue; //Could still turn out to be the HTTP/2 preface.
                    }

                    if (NpHostRouter.TryReadHost(data, out host))
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (_abort.IsCancellationRequested == false)
            {
                Singletons.Logging.Write(NpLogging.Severity.Verbose,
                    $"{_clientDisplay} did not send a request on {_listener.Description} in time and is closed.");
                return false;
            }

            if (held.Length == 0)
            {
                return false; //The client went away without saying anything.
            }

            var proxy = NpHostRouter.Match(_listener.Routes, host);

            if (proxy == null)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"No proxy on {_listener.Description} answers to the host name '{host ?? "(none)"}' asked for by {_clientDisplay}.");

                try
                {
                    await _clientStream!.WriteAsync("HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), _abort.Token);
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
                {
                    //The client is gone.
                }

                return false;
            }

            _proxy = proxy;
            _hostName = host;
            _initialClientBytes = held.ToArray();
            _clientIsHttp2 = isHttp2 && proxy.Configuration.AllowHttp2;
            _peeked = true;
            return true;
        }

        /// <summary>
        /// Closes the connection when it has carried no data, in either direction, for the idle timeout of the proxy.
        /// </summary>
        private async Task IdleWatchAsync()
        {
            var seconds = _proxy.Configuration.IdleTimeoutSeconds;
            if (seconds <= 0)
            {
                return;
            }

            var limit = TimeSpan.FromSeconds(seconds);
            var interval = TimeSpan.FromSeconds(Math.Clamp(seconds / 4.0, 0.25, 5));

            try
            {
                while (true)
                {
                    await Task.Delay(interval, _abort.Token);

                    if (DateTime.UtcNow - LastActivityDateTime >= limit)
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Verbose,
                            $"Connection {Id} from {_clientDisplay} on proxy '{_proxy.Configuration.Name}' was idle for {seconds} seconds and is closed.");
                        _disconnectReason = NpDisconnectReason.IdleTimeout;
                        await _plugins.IdleTimeoutAsync(this);
                        Abort();
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                //The connection is over.
            }
        }

        /// <summary>
        /// A plain (not TLS) HTTP listener that allows HTTP/2 has to look at the start of the data from the client to tell if it is HTTP/2
        /// without TLS (h2c with prior knowledge, which is what gRPC clients use), because the endpoint has to be connected the same way.
        /// Returns false if the client goes away before it says anything.
        /// </summary>
        private async Task<bool> DetectClientHttp2Async()
        {
            if (_peeked || _proxy.TerminatesTls || _proxy.EffectiveTrafficType != TrafficType.Http || _proxy.Configuration.AllowHttp2 == false)
            {
                return true;
            }

            var preface = Http2StreamParser.ClientPrefaceBytes;
            var held = new byte[preface.Length];
            var count = 0;

            while (count < preface.Length)
            {
                var read = await _clientStream!.ReadAsync(held.AsMemory(count), _abort.Token);
                if (read <= 0)
                {
                    break;
                }

                Touch();
                count += read;

                if (held.AsSpan(0, count).SequenceEqual(preface.AsSpan(0, count)) == false)
                {
                    break; //Not HTTP/2.
                }
            }

            if (count == 0)
            {
                return false;
            }

            _clientIsHttp2 = count == preface.Length && held.AsSpan().SequenceEqual(preface);
            _initialClientBytes = held.AsSpan(0, count).ToArray();
            return true;
        }

        /// <summary>
        /// Chooses the endpoint (see <see cref="NpProxy.GetEndpointAttemptOrder"/>) and connects to it, going on to the next endpoint
        /// for as long as they fail. Returns false (after logging why) when none of them can be connected to.
        /// </summary>
        private async Task<bool> ConnectToEndpointAsync()
        {
            var candidates = _proxy.GetEndpointAttemptOrder(_clientKey);
            var failures = new List<string>();

            foreach (var endpoint in candidates)
            {
                _abort.Token.ThrowIfCancellationRequested();

                var endpointInfo = new NpEndpointInfo(endpoint.Id, endpoint.Address, endpoint.Port, endpoint.TrafficType == TrafficType.Https);

                var verdict = await _plugins.EndpointConnectingAsync(this, endpointInfo);
                if (verdict.Allowed == false)
                {
                    failures.Add($"{NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}: skipped. {verdict.Reason}");
                    continue;
                }

                var statistics = _proxy.EndpointStatistics.GetOrAdd(endpoint.Id, _ => new NpProxyEndpointStatistics());
                statistics.Reserve();

                TcpClient? tcpClient = null;

                try
                {
                    tcpClient = await NpNetwork.ConnectAsync(endpoint.Address, endpoint.Port, _endpointConnectTimeout, _abort.Token);
                    Stream stream = tcpClient.GetStream();

                    if (endpoint.TrafficType == TrafficType.Https)
                    {
                        stream = await StartEndpointTlsAsync(endpoint, stream);
                    }

                    _endpointClient = tcpClient;
                    _endpointStream = stream;
                    _endpointStatistics = statistics;

                    //The address that was connected to, which is not what is configured when that is a host name.
                    _endpointPort = endpoint.Port;
                    _endpointAddress = tcpClient.Client.RemoteEndPoint is IPEndPoint endpointRemote
                        ? NpNetwork.Unmap(endpointRemote.Address).ToString() : endpoint.Address;

                    statistics.Established();
                    _proxy.RememberStickySession(_clientKey, endpoint);

                    _endpointInfo = endpointInfo;
                    await _plugins.EndpointConnectedAsync(this, endpointInfo);

                    Singletons.Logging.Write(NpLogging.Severity.Verbose,
                        $"Connection {Id} from {_clientDisplay} is connected to endpoint {NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}.");

                    return true;
                }
                catch (OperationCanceledException) when (_abort.IsCancellationRequested)
                {
                    statistics.ReleaseFailed();
                    try { tcpClient?.Dispose(); } catch { }
                    throw;
                }
                catch (Exception ex)
                {
                    var wasFailing = statistics.LastFailure != null;
                    statistics.ReleaseFailed();
                    try { tcpClient?.Dispose(); } catch { }

                    var description = $"{NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}: {ex.GetBaseException().Message}";
                    failures.Add(description);

                    await _plugins.EndpointConnectFailedAsync(this, endpointInfo, ex.GetBaseException().Message);

                    //Say so once when an endpoint goes down, not for every connection that finds it down.
                    Singletons.Logging.Write(wasFailing ? NpLogging.Severity.Verbose : NpLogging.Severity.Warning,
                        $"Proxy '{_proxy.Configuration.Name}' could not connect to endpoint {description}");
                }
            }

            Singletons.Logging.Write(NpLogging.Severity.Warning,
                $"Proxy '{_proxy.Configuration.Name}' could not connect {_clientDisplay} to any endpoint"
                + (failures.Count > 0 ? $" ({string.Join("; ", failures)})." : " (there are none enabled)."));

            return false;
        }

        /// <summary>
        /// Starts TLS with an endpoint, validating its certificate against the address of the endpoint.
        /// </summary>
        private async Task<Stream> StartEndpointTlsAsync(NpEndpoint endpoint, Stream stream)
        {
            var ssl = new SslStream(stream, false);

            var options = new SslClientAuthenticationOptions
            {
                TargetHost = NpNetwork.NormalizeHost(endpoint.Address)
            };

            //The endpoint has to speak what the client speaks: the bytes of an HTTP/2 client are passed on as they are.
            if (_clientIsHttp2)
            {
                options.ApplicationProtocols = [SslApplicationProtocol.Http2];
            }
            else if (_proxy.Configuration.AllowHttp2)
            {
                options.ApplicationProtocols = [SslApplicationProtocol.Http11];
            }

            if (!endpoint.TlsValidateSSLCertificate)
            {
                options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
            timeout.CancelAfter(_handshakeTimeout);

            try
            {
                await ssl.AuthenticateAsClientAsync(options, timeout.Token);

                if (_clientIsHttp2 && ssl.NegotiatedApplicationProtocol != SslApplicationProtocol.Http2)
                {
                    throw new AuthenticationException("The client is using HTTP/2 but the endpoint did not agree to it. Turn off"
                        + " [Allow HTTP/2] on the proxy, or enable HTTP/2 on the endpoint.");
                }

                return ssl;
            }
            catch (AuthenticationException ex) when (_clientIsHttp2 && ex.Message.Contains("did not agree") == false)
            {
                ssl.Dispose();

                //An endpoint that does not do HTTP/2 can refuse the handshake with a message that does not say why.
                throw new AuthenticationException("The client is using HTTP/2 and the TLS handshake with the endpoint failed. This usually means"
                    + $" the endpoint does not support HTTP/2. ({ex.InnerException?.Message ?? ex.Message})", ex);
            }
            catch
            {
                ssl.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Copies data one way until the sender is finished (or the connection is aborted), applying the HTTP header rules on the way.
        /// </summary>
        private async Task PumpAsync(Stream from, Stream to, ConnectionDirection direction, byte[]? initialBytes = null)
        {
            var configuration = _proxy.Configuration;
            var buffer = new PumpBuffer(Math.Max(configuration.InitialBufferSize, initialBytes?.Length ?? 0));
            var parser = CreateHttpParser(direction);
            var dataDirection = direction == ConnectionDirection.Inbound ? NpDataDirection.ClientToEndpoint : NpDataDirection.EndpointToClient;

            try
            {
                while (true)
                {
                    int read;
                    if (initialBytes != null)
                    {
                        //Bytes that were already read from the client to tell what it speaks.
                        initialBytes.CopyTo(buffer.Bytes, 0);
                        read = initialBytes.Length;
                        initialBytes = null;
                    }
                    else
                    {
                        read = await from.ReadAsync(buffer.Bytes.AsMemory(), _abort.Token);
                    }

                    if (read <= 0)
                    {
                        FinishSending(to, direction);
                        return;
                    }

                    buffer.Length = read;
                    Touch();

                    if (direction == ConnectionDirection.Inbound)
                    {
                        Interlocked.Add(ref _bytesIn, read);
                        _proxy.Statistics.AddBytesRead(read);
                    }
                    else
                    {
                        Interlocked.Add(ref _bytesOut, read);
                        _endpointStatistics?.AddBytesRead(read);
                    }

                    var processable = read;

                    //The plug-ins see the data as it is between the two sides: what was read from a TLS side is decrypted already.
                    if (_plugins.HasDataHooks)
                    {
                        var received = new NpDataEventArgs(this, dataDirection, buffer.Bytes.AsMemory(0, read), from is SslStream, to is SslStream);
                        await _plugins.DataReceivedAsync(received);

                        if (received.CloseReason != null)
                        {
                            ClosedByPlugin(received.CloseReason);
                            return;
                        }

                        if (received.IsDropped || (received.IsModified && received.Data.Length == 0))
                        {
                            buffer.AutoResize(configuration.MaxBufferSize);
                            continue;
                        }

                        if (received.IsModified)
                        {
                            if (received.Data.Length > buffer.Bytes.Length)
                            {
                                buffer.Bytes = new byte[received.Data.Length];
                            }

                            received.Data.CopyTo(buffer.Bytes);
                            processable = received.Data.Length;
                        }
                    }

                    var (bytes, length) = parser != null ? parser.Process(buffer.Bytes, processable) : (buffer.Bytes, processable);

                    if (length > 0 && _plugins.HasDataHooks)
                    {
                        var sending = new NpDataEventArgs(this, dataDirection, bytes.AsMemory(0, length), from is SslStream, to is SslStream);
                        await _plugins.DataSendingAsync(sending);

                        if (sending.CloseReason != null)
                        {
                            ClosedByPlugin(sending.CloseReason);
                            return;
                        }

                        if (sending.IsDropped)
                        {
                            length = 0;
                        }
                        else if (sending.IsModified)
                        {
                            bytes = sending.Data.ToArray();
                            length = bytes.Length;
                        }
                    }

                    if (length > 0) //Nothing to send while a partial HTTP header is held.
                    {
                        await to.WriteAsync(bytes.AsMemory(0, length), _abort.Token);
                        Touch();

                        if (direction == ConnectionDirection.Inbound)
                        {
                            _endpointStatistics?.AddBytesWritten(length);
                        }
                        else
                        {
                            _proxy.Statistics.AddBytesWritten(length);
                        }

                        if (_plugins.HasDataSentHook)
                        {
                            await _plugins.DataSentAsync(this, dataDirection, length);
                        }
                    }

                    buffer.AutoResize(configuration.MaxBufferSize);
                }
            }
            catch (OperationCanceledException)
            {
                //Aborted.
                MarkEnded(NpDisconnectReason.Aborted);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                MarkEnded(NpDisconnectReason.Aborted);
                Singletons.Logging.Write(NpLogging.Severity.Verbose,
                    $"Connection {Id} from {_clientDisplay} ({(direction == ConnectionDirection.Inbound ? "client to endpoint" : "endpoint to client")}) ended: {ex.GetBaseException().Message}");
                Abort();
            }
            catch (Exception ex)
            {
                MarkEnded(NpDisconnectReason.Error);
                Singletons.Logging.Write($"Connection {Id} from {_clientDisplay} failed", ex);
                await _plugins.ErrorAsync(new NpPluginError($"Connection {Id} from {_clientDisplay} failed: {ex.GetBaseException().Message}", ex, _proxy.Info, this));
                Abort();
            }
        }

        /// <summary>
        /// A plug-in asked for the connection to be closed from a data event.
        /// </summary>
        private void ClosedByPlugin(string reason)
        {
            _disconnectReason = NpDisconnectReason.ClosedByPlugin;
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} was closed by a plugin: {reason}");
            Abort();
        }

        /// <summary>
        /// One side has finished sending. Tell the other side, so that (for example) a client that is done with its request still
        /// receives the whole response. A TLS session cannot be half closed, so the connection ends instead.
        /// </summary>
        private void FinishSending(Stream to, ConnectionDirection direction)
        {
            try
            {
                if (to is NetworkStream)
                {
                    var socket = direction == ConnectionDirection.Inbound ? _endpointClient?.Client : _client.Client;
                    socket?.Shutdown(SocketShutdown.Send);
                }
                else
                {
                    Abort();
                }
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
            {
                Abort();
            }
        }

        /// <summary>
        /// Follows the HTTP stream in one direction when there are header rules for it: requests (from the client) with request rules,
        /// responses (from the endpoint) with response rules. This applies to HTTP, and to HTTPS where the listener terminates TLS.
        /// </summary>
        private IHttpStreamProcessor? CreateHttpParser(ConnectionDirection direction)
        {
            if (_proxy.EffectiveTrafficType != TrafficType.Http && _proxy.EffectiveTrafficType != TrafficType.Https)
            {
                return null;
            }

            var wanted = direction == ConnectionDirection.Inbound ? HttpHeaderType.Request : HttpHeaderType.Response;

            var hasRules = _proxy.Configuration.HttpHeaderRules.Collection
                .Any(o => o.Enabled && (o.HeaderType == wanted || o.HeaderType == HttpHeaderType.Any));

            //Tells the endpoint who the client is: the address it connected from and whether it used TLS.
            var forwarded = direction == ConnectionDirection.Inbound && _proxy.Configuration.ForwardedHeaders != ForwardedHeadersMode.Off
                ? new NpForwardedHeaders(_proxy.Configuration.ForwardedHeaders, _clientKey, _proxy.TerminatesTls ? "https" : "http")
                : null;

            if (_clientIsHttp2)
            {
                //The scheme of a request has to match what the endpoint connection is, which is not always what the client's was.
                var clientScheme = _proxy.TerminatesTls ? "https" : "http";
                var endpointScheme = _endpointStream is SslStream ? "https" : "http";
                var fixScheme = direction == ConnectionDirection.Inbound && clientScheme != endpointScheme;

                return hasRules || fixScheme || forwarded != null
                    ? new Http2StreamParser(_proxy.Configuration, fromClient: direction == ConnectionDirection.Inbound)
                    { RequestScheme = fixScheme ? endpointScheme : null, Forwarded = forwarded }
                    : null;
            }

            return hasRules || forwarded != null ? new HttpStreamParser(_proxy.Configuration, direction, forwarded) : null;
        }

        private void CloseSockets()
        {
            try { _abort.Cancel(); } catch (ObjectDisposedException) { }

            try { _clientStream?.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
            try { _endpointStream?.Dispose(); } catch { }
            try { _endpointClient?.Dispose(); } catch { }
        }

        /// <summary>
        /// The connection is over: it no longer counts against the proxy.
        /// </summary>
        private void Finish()
        {
            _endpointStatistics?.ConnectionClosed();

            if (_admitted)
            {
                _proxy.Statistics.ConnectionClosed();
                _proxy.RemoveConnection(this);
                _proxy.ReleaseClient(_clientKey);
            }

            try { _abort.Dispose(); } catch { }

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} is closed.");

            _completion.TrySetResult();
        }
    }
}
