using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Http;
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

        private Stream? _clientStream;
        private TcpClient? _endpointClient;
        private Stream? _endpointStream;
        private NpProxyEndpointStatistics? _endpointStatistics;

        public Guid Id { get; } = Guid.NewGuid();
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

        private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        /// <summary>
        /// Runs the connection from start to finish. It does not throw: whatever goes wrong is logged and ends the connection.
        /// </summary>
        public async Task RunAsync()
        {
            _proxy.Statistics.ConnectionOpened();

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Proxy '{_proxy.Configuration.Name}' accepted {_clientDisplay} ({Id}).");

            try
            {
                if (await TerminateClientTlsAsync() == false)
                {
                    return;
                }

                if (await ConnectToEndpointAsync() == false)
                {
                    return;
                }

                await Task.WhenAll(
                    PumpAsync(_clientStream!, _endpointStream!, fromClient: true),
                    PumpAsync(_endpointStream!, _clientStream!, fromClient: false));
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
                    ApplicationProtocols = [SslApplicationProtocol.Http11],
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.None, //Let the operating system choose.
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, timeout.Token);

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

            if (!endpoint.TlsIgnoreCertificateErrors)
            {
                options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_abort.Token);
            timeout.CancelAfter(_handshakeTimeout);

            try
            {
                await ssl.AuthenticateAsClientAsync(options, timeout.Token);
                return ssl;
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
        private async Task PumpAsync(Stream from, Stream to, bool fromClient)
        {
            var configuration = _proxy.Configuration;
            var buffer = new PumpBuffer(configuration.InitialBufferSize);
            var parser = CreateHttpParser(fromClient);

            try
            {
                while (true)
                {
                    int read = await from.ReadAsync(buffer.Bytes.AsMemory(), _abort.Token);
                    if (read <= 0)
                    {
                        FinishSending(to, fromClient);
                        return;
                    }

                    buffer.Length = read;
                    Touch();

                    var (bytes, length) = parser != null ? parser.Process(buffer.Bytes, read) : (buffer.Bytes, read);

                    if (fromClient)
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

                        if (fromClient)
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
                    $"Connection {Id} from {_clientDisplay} ({(fromClient ? "client to endpoint" : "endpoint to client")}) ended: {ex.GetBaseException().Message}");
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
        private void FinishSending(Stream to, bool fromClient)
        {
            try
            {
                if (to is NetworkStream)
                {
                    var socket = fromClient ? _endpointClient?.Client : _client.Client;
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
        private IHttpStreamProcessor? CreateHttpParser(bool fromClient)
        {
            if (_proxy.EffectiveTrafficType != TrafficType.Http && _proxy.EffectiveTrafficType != TrafficType.Https)
            {
                return null;
            }

            var wanted = fromClient ? HttpHeaderType.Request : HttpHeaderType.Response;

            var hasRules = _proxy.Configuration.HttpHeaderRules.Collection
                .Any(o => o.Enabled && (o.HeaderType == wanted || o.HeaderType == HttpHeaderType.Any));

            return hasRules ? new HttpStreamParser(_proxy.Configuration, isInbound: fromClient) : null;
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

            try { _abort.Dispose(); } catch { }

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Connection {Id} from {_clientDisplay} is closed.");

            _completion.TrySetResult();
        }
    }
}
