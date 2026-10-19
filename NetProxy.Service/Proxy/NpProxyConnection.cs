using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Http;
using NetProxy.Service.Http.Http2;
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
    internal sealed class NpProxyConnection
    {
        /// <summary>
        /// A TLS handshake must complete within this time, so that a client that connects and goes quiet does not hold the connection.
        /// </summary>
        private static readonly TimeSpan _handshakeTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long to wait on one endpoint to accept a connection before trying the next.
        /// </summary>
        private static readonly TimeSpan _endpointConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly NpProxy _proxy;
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

        private Stream? _clientStream;
        private TcpClient? _endpointClient;
        private Stream? _endpointStream;
        private NpProxyEndpointStatistics? _endpointStatistics;

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

        public NpProxyConnection(NpProxy proxy, TcpClient client)
        {
            _proxy = proxy;
            _client = client;
            _abort = CancellationTokenSource.CreateLinkedTokenSource(proxy.StopToken);

            if (client.Client.RemoteEndPoint is IPEndPoint remote)
            {
                //Clients that connected over IPv4 to a dual-stack listener are shown (and remembered) by their IPv4 address.
                _clientKey = NpNetwork.Unmap(remote.Address).ToString();
                _clientDisplay = NpNetwork.FormatHostPort(_clientKey, remote.Port);
            }
            else
            {
                _clientKey = $"{client.Client.RemoteEndPoint}";
                _clientDisplay = _clientKey;
            }
        }

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
        /// Drops a connection that was never started, because the proxy is at one of its connection limits.
        /// </summary>
        public void Refuse()
        {
            try { _client.Dispose(); } catch { }
            try { _abort.Dispose(); } catch { }
        }

        private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        /// <summary>
        /// Runs the connection from start to finish. It does not throw: whatever goes wrong is logged and ends the connection.
        /// </summary>
        public async Task RunAsync()
        {
            _proxy.Statistics.ConnectionOpened();

            _ = IdleWatchAsync();

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Proxy '{_proxy.Configuration.Name}' accepted {_clientDisplay} ({Id}).");

            try
            {
                if (await TerminateClientTlsAsync() == false)
                {
                    return;
                }

                if (await DetectClientHttp2Async() == false)
                {
                    return;
                }

                if (await ConnectToEndpointAsync() == false)
                {
                    return;
                }

                await Task.WhenAll(
                    PumpAsync(_clientStream!, _endpointStream!, direction: ConnectionDirection.Inbound, _initialClientBytes),
                    PumpAsync(_endpointStream!, _clientStream!, direction: ConnectionDirection.Outbound));
            }
            catch (OperationCanceledException)
            {
                //The proxy is stopping, or the connection was aborted.
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or AuthenticationException)
            {
                Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} ended: {ex.GetBaseException().Message}");
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write($"Connection {Id} from {_clientDisplay} failed", ex);
            }
            finally
            {
                Close();
            }
        }

        /// <summary>
        /// When the listener terminates TLS, completes the handshake with the client. Returns false (after logging why) if it fails.
        /// </summary>
        private async Task<bool> TerminateClientTlsAsync()
        {
            Stream stream = _client.GetStream();

            if (_proxy.TerminatesTls == false)
            {
                _clientStream = stream;
                return true;
            }

            var ssl = new SslStream(stream, false);
            _clientStream = ssl;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
            timeout.CancelAfter(_handshakeTimeout);

            try
            {
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _proxy.Certificate,
                    ApplicationProtocols = _proxy.Configuration.AllowHttp2
                        ? [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11]
                        : [SslApplicationProtocol.Http11],
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.None, //Let the operating system choose.
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, timeout.Token);

                _clientIsHttp2 = ssl.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2;
                return true;
            }
            catch (AuthenticationException ex)
            {
                //Usually a client that does not trust the certificate and hung up.
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"TLS handshake with {_clientDisplay} failed on proxy '{_proxy.Configuration.Name}': {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                if (_abort.IsCancellationRequested == false)
                {
                    //Browsers routinely open connections that they drop before the handshake is done.
                    Singletons.Logging.Write(NpLogging.Severity.Verbose,
                        $"TLS handshake with {_clientDisplay} on proxy '{_proxy.Configuration.Name}' did not complete: {ex.GetBaseException().Message}");
                }
                return false;
            }
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
            if (_proxy.TerminatesTls || _proxy.EffectiveTrafficType != TrafficType.Http || _proxy.Configuration.AllowHttp2 == false)
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

                    statistics.Established();
                    _proxy.RememberStickySession(_clientKey, endpoint);

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

                    var (bytes, length) = parser != null ? parser.Process(buffer.Bytes, read) : (buffer.Bytes, read);

                    if (direction == ConnectionDirection.Inbound)
                    {
                        _proxy.Statistics.AddBytesRead(read);
                    }
                    else
                    {
                        _endpointStatistics?.AddBytesRead(read);
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
                    }

                    buffer.AutoResize(configuration.MaxBufferSize);
                }
            }
            catch (OperationCanceledException)
            {
                //Aborted.
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                Singletons.Logging.Write(NpLogging.Severity.Verbose,
                    $"Connection {Id} from {_clientDisplay} ({(direction == ConnectionDirection.Inbound ? "client to endpoint" : "endpoint to client")}) ended: {ex.GetBaseException().Message}");
                Abort();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write($"Connection {Id} from {_clientDisplay} failed", ex);
                Abort();
            }
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

            if (_clientIsHttp2)
            {
                //The scheme of a request has to match what the endpoint connection is, which is not always what the client's was.
                var clientScheme = _proxy.TerminatesTls ? "https" : "http";
                var endpointScheme = _endpointStream is SslStream ? "https" : "http";
                var fixScheme = direction == ConnectionDirection.Inbound && clientScheme != endpointScheme;

                return hasRules || fixScheme
                    ? new Http2StreamParser(_proxy.Configuration, fromClient: direction == ConnectionDirection.Inbound)
                    { RequestScheme = fixScheme ? endpointScheme : null }
                    : null;
            }

            return hasRules ? new HttpStreamParser(_proxy.Configuration, direction) : null;
        }

        private void Close()
        {
            try { _abort.Cancel(); } catch (ObjectDisposedException) { }

            try { _clientStream?.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
            try { _endpointStream?.Dispose(); } catch { }
            try { _endpointClient?.Dispose(); } catch { }

            _endpointStatistics?.ConnectionClosed();
            _proxy.Statistics.ConnectionClosed();
            _proxy.RemoveConnection(this);
            _proxy.ReleaseClient(_clientKey);

            try { _abort.Dispose(); } catch { }

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} is closed.");

            _completion.TrySetResult();
        }
    }
}
