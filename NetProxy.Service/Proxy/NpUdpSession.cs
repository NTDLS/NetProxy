using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using NetProxy.Service.Plugins;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// What stands in for a connection with a UDP client: the datagrams of one client address and port, relayed to one endpoint over a
    /// socket of their own (so that what the endpoint answers can be told from the answers to other clients). It lasts until the client has
    /// been quiet for the idle timeout of the proxy, or the proxy is stopped, or a plugin or the endpoint ends it.
    ///
    /// The datagrams of the client are queued and relayed in order by the task of the session, so that a plugin that is slow holds up its
    /// own client and nobody else, and the queue is limited, so that a flood from one address cannot use the memory of the service.
    /// </summary>
    internal sealed class NpUdpSession : INpConnection, INpActiveConnection
    {
        public enum EnqueueResult { Queued, Dropped, Ended }

        private const int QueueCapacity = 1024;
        private const long MaxQueuedBytes = 1024 * 1024;

        /// <summary>
        /// How long to wait for an endpoint to be looked up and have a socket made for it before trying the next.
        /// </summary>
        private static readonly TimeSpan _endpointConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly record struct Datagram(byte[] Buffer, int Length);

        private readonly NpUdpListener _listener;
        private readonly NpProxy _proxy;
        private readonly IPEndPoint _client;
        private readonly string _clientKey;
        private readonly CancellationTokenSource _abort;
        private readonly Channel<Datagram> _queue = Channel.CreateBounded<Datagram>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait //TryWrite fails when it is full, which is a dropped datagram.
        });
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<string, object?> _state = new();

        private NpPluginSet _plugins = NpPluginSet.Empty;
        private Socket? _endpointSocket;
        private NpEndpointInfo? _endpointInfo;
        private NpProxyEndpointStatistics? _endpointStatistics;
        private IPAddress? _endpointAddress;
        private Task? _endpointTask;
        private NpDisconnectReason _reason = NpDisconnectReason.Completed;
        private bool _connecting;
        private volatile bool _ended;
        private int _expiring;
        private long _queuedBytes;
        private long _lastActivityTicks = DateTime.UtcNow.Ticks;
        private long _bytesIn;
        private long _bytesOut;
        private long _datagramsDropped;
        private long _windowStartTicks;
        private long _windowCount;

        public Guid Id { get; } = Guid.NewGuid();
        public DateTime StartDateTime { get; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);
        public Task Completion => _completion.Task;

        /// <summary>
        /// The client as the session table knows it (an IPv4 client of a dual-stack socket is shown by its IPv4 address).
        /// </summary>
        public IPEndPoint Key { get; }

        /// <summary>
        /// The datagrams of this client that were thrown away: over the rate limit, or more than the session could hold.
        /// </summary>
        public long DatagramsDropped => Interlocked.Read(ref _datagramsDropped);

        public NpUdpSession(NpUdpListener listener, NpProxy proxy, IPEndPoint client, IPEndPoint key, string clientKey)
        {
            _listener = listener;
            _proxy = proxy;
            _client = client;
            Key = key;
            _clientKey = clientKey;
            _abort = CancellationTokenSource.CreateLinkedTokenSource(proxy.StopToken, listener.StopToken);
        }

        private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        //---------------------------------------------------------------- Taking datagrams in (called by the listener, one at a time)

        /// <summary>
        /// Queues a datagram of the client to be relayed to the endpoint. The datagram is copied.
        /// </summary>
        public EnqueueResult Enqueue(byte[] buffer, int length)
        {
            if (_ended)
            {
                return EnqueueResult.Ended;
            }

            Touch();

            var limit = _proxy.Configuration.MaxDatagramsPerSecondPerClient;
            if (limit > 0)
            {
                var now = DateTime.UtcNow.Ticks;
                if (now - _windowStartTicks >= TimeSpan.TicksPerSecond)
                {
                    _windowStartTicks = now;
                    _windowCount = 0;
                }

                if (++_windowCount > limit)
                {
                    Interlocked.Increment(ref _datagramsDropped);
                    return EnqueueResult.Dropped;
                }
            }

            if (Interlocked.Read(ref _queuedBytes) + length > MaxQueuedBytes)
            {
                Interlocked.Increment(ref _datagramsDropped);
                return EnqueueResult.Dropped;
            }

            var copy = ArrayPool<byte>.Shared.Rent(Math.Max(length, 1));
            Buffer.BlockCopy(buffer, 0, copy, 0, length);

            if (_queue.Writer.TryWrite(new Datagram(copy, length)) == false)
            {
                ArrayPool<byte>.Shared.Return(copy);

                if (_ended)
                {
                    return EnqueueResult.Ended;
                }

                Interlocked.Increment(ref _datagramsDropped);
                return EnqueueResult.Dropped;
            }

            Interlocked.Add(ref _queuedBytes, length);
            return EnqueueResult.Queued;
        }

        /// <summary>
        /// Starts relaying. The session is counted against the proxy from here until it is over.
        /// </summary>
        public void Start()
        {
            _plugins = _proxy.Plugins;
            _proxy.AddConnection(this);
            _proxy.Statistics.ConnectionOpened();

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Proxy '{_proxy.Configuration.Name}' started a UDP session for {NpNetwork.FormatHostPort(_clientKey, Key.Port)} ({Id}).");

            _ = Task.Run(RunAsync);
        }

        //---------------------------------------------------------------- The life of the session

        private async Task RunAsync()
        {
            try
            {
                _connecting = true;

                var verdict = await _plugins.ConnectingAsync(this);
                if (verdict.Allowed == false)
                {
                    _reason = NpDisconnectReason.Denied;
                    _listener.Refuse(_clientKey);
                    Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} was not allowed. {verdict.Reason}");
                    return;
                }

                if (await ConnectToEndpointAsync() == false)
                {
                    _reason = NpDisconnectReason.EndpointUnavailable;
                    _listener.Refuse(_clientKey);
                    return;
                }

                await _plugins.ConnectedAsync(this);

                _endpointTask = Task.Run(RelayEndpointToClientAsync);

                await foreach (var datagram in _queue.Reader.ReadAllAsync(_abort.Token))
                {
                    try
                    {
                        await RelayClientToEndpointAsync(datagram);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(datagram.Buffer);
                        Interlocked.Add(ref _queuedBytes, -datagram.Length);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                MarkEnded(NpDisconnectReason.Aborted);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                MarkEnded(NpDisconnectReason.Aborted);
                Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} ended: {ex.GetBaseException().Message}");
            }
            catch (Exception ex)
            {
                MarkEnded(NpDisconnectReason.Error);
                Singletons.Logging.Write($"UDP session {Id} from {_clientKey} failed", ex);
                await _plugins.ErrorAsync(new NpPluginError($"UDP session {Id} from {_clientKey} failed: {ex.GetBaseException().Message}", ex, _proxy.Info, this));
            }
            finally
            {
                await EndAsync();
            }
        }

        private async Task EndAsync()
        {
            _ended = true;
            _queue.Writer.TryComplete();

            if (_connecting)
            {
                await _plugins.DisconnectingAsync(this, _reason);
            }

            Abort();

            try
            {
                if (_endpointTask != null)
                {
                    await _endpointTask.WaitAsync(TimeSpan.FromSeconds(2));
                }
            }
            catch
            {
                //It is over either way.
            }

            if (_connecting)
            {
                await _plugins.DisconnectedAsync(this, new NpDisconnectInfo(_reason, DateTime.UtcNow - StartDateTime,
                    Interlocked.Read(ref _bytesIn), Interlocked.Read(ref _bytesOut)));
            }

            while (_queue.Reader.TryRead(out var left))
            {
                ArrayPool<byte>.Shared.Return(left.Buffer);
            }

            _endpointStatistics?.ConnectionClosed();
            _proxy.Statistics.ConnectionClosed();
            _proxy.RemoveConnection(this);
            _proxy.ReleaseClient(_clientKey);
            _listener.Remove(this);

            try { _abort.Dispose(); } catch { }

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} is closed.");

            _completion.TrySetResult();
        }

        private void MarkEnded(NpDisconnectReason reason)
        {
            if (_reason == NpDisconnectReason.Completed)
            {
                _reason = reason;
            }
        }

        /// <summary>
        /// Ends the session. Whatever is waiting on one of its sockets is released.
        /// </summary>
        public void Abort()
        {
            try { _abort.Cancel(); } catch (ObjectDisposedException) { }
            _queue.Writer.TryComplete();
            try { _endpointSocket?.Dispose(); } catch { }
        }

        /// <summary>
        /// The client has been quiet for the idle timeout.
        /// </summary>
        public async Task ExpireAsync(int seconds)
        {
            if (_ended || Interlocked.Exchange(ref _expiring, 1) != 0)
            {
                return;
            }

            _reason = NpDisconnectReason.IdleTimeout;
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} had no datagrams for {seconds} seconds and is closed.");

            await _plugins.IdleTimeoutAsync(this);
            Abort();
        }

        //---------------------------------------------------------------- Choosing the endpoint

        /// <summary>
        /// Chooses the endpoint (see <see cref="NpProxy.GetEndpointAttemptOrder"/>) and makes a socket for it, going on to the next endpoint
        /// for as long as that fails. UDP says nothing about whether the other side is there, so this only fails for what can be seen
        /// from here (a name that does not resolve). An endpoint that is down is found out later, when its ICMP answer comes back.
        /// </summary>
        private async Task<bool> ConnectToEndpointAsync()
        {
            var failures = new List<string>();

            foreach (var endpoint in _proxy.GetEndpointAttemptOrder(_clientKey))
            {
                _abort.Token.ThrowIfCancellationRequested();

                var info = new NpEndpointInfo(endpoint.Id, endpoint.Address, endpoint.Port, false);

                var verdict = await _plugins.EndpointConnectingAsync(this, info);
                if (verdict.Allowed == false)
                {
                    failures.Add($"{NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}: skipped. {verdict.Reason}");
                    continue;
                }

                var statistics = _proxy.EndpointStatistics.GetOrAdd(endpoint.Id, _ => new NpProxyEndpointStatistics());
                statistics.Reserve();

                try
                {
                    var socket = await NpNetwork.ConnectUdpAsync(endpoint.Address, endpoint.Port, _endpointConnectTimeout, _abort.Token);

                    _endpointSocket = socket;
                    _endpointStatistics = statistics;
                    _endpointInfo = info;
                    _endpointAddress = socket.RemoteEndPoint is IPEndPoint remote ? NpNetwork.Unmap(remote.Address) : null;

                    statistics.Established();
                    _proxy.RememberStickySession(_clientKey, endpoint);

                    Singletons.Logging.Write(NpLogging.Severity.Verbose,
                        $"UDP session {Id} from {_clientKey} is relayed to endpoint {NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}.");

                    await _plugins.EndpointConnectedAsync(this, info);
                    return true;
                }
                catch (OperationCanceledException) when (_abort.IsCancellationRequested)
                {
                    statistics.ReleaseFailed();
                    throw;
                }
                catch (Exception ex)
                {
                    var wasFailing = statistics.LastFailure != null;
                    statistics.ReleaseFailed();

                    var description = $"{NpNetwork.FormatHostPort(endpoint.Address, endpoint.Port)}: {ex.GetBaseException().Message}";
                    failures.Add(description);

                    Singletons.Logging.Write(wasFailing ? NpLogging.Severity.Verbose : NpLogging.Severity.Warning,
                        $"Proxy '{_proxy.Configuration.Name}' could not set up UDP endpoint {description}");

                    await _plugins.EndpointConnectFailedAsync(this, info, ex.GetBaseException().Message);
                }
            }

            Singletons.Logging.Write(NpLogging.Severity.Warning,
                $"Proxy '{_proxy.Configuration.Name}' could not relay {_clientKey} to any endpoint"
                + (failures.Count > 0 ? $" ({string.Join("; ", failures)})." : " (there are none enabled)."));

            return false;
        }

        //---------------------------------------------------------------- The datagrams

        /// <summary>
        /// Takes a datagram through the plugins. Returns the data to send, or null if it is not to be sent (a plugin dropped it, or ended
        /// the session, which is done here).
        /// </summary>
        private async ValueTask<ReadOnlyMemory<byte>?> ThroughPluginsAsync(ReadOnlyMemory<byte> data, NpDataDirection direction)
        {
            if (_plugins.HasDataHooks == false)
            {
                return data;
            }

            //A datagram is not encrypted by this proxy on either side.
            var received = new NpDataEventArgs(this, direction, data, receivedEncrypted: false, willBeSentEncrypted: false);
            await _plugins.DataReceivedAsync(received);

            if (received.CloseReason != null)
            {
                ClosedByPlugin(received.CloseReason);
                return null;
            }

            if (received.IsDropped)
            {
                return null;
            }

            var sending = new NpDataEventArgs(this, direction, received.Data, receivedEncrypted: false, willBeSentEncrypted: false);
            await _plugins.DataSendingAsync(sending);

            if (sending.CloseReason != null)
            {
                ClosedByPlugin(sending.CloseReason);
                return null;
            }

            return sending.IsDropped ? null : sending.Data;
        }

        private void ClosedByPlugin(string reason)
        {
            _reason = NpDisconnectReason.ClosedByPlugin;
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} was closed by a plugin: {reason}");
            Abort();
        }

        private async Task RelayClientToEndpointAsync(Datagram datagram)
        {
            Interlocked.Add(ref _bytesIn, datagram.Length);
            _proxy.Statistics.AddBytesRead(datagram.Length);

            var data = await ThroughPluginsAsync(datagram.Buffer.AsMemory(0, datagram.Length), NpDataDirection.ClientToEndpoint);
            if (data == null)
            {
                return;
            }

            try
            {
                await _endpointSocket!.SendAsync(data.Value, SocketFlags.None, _abort.Token);
            }
            catch (SocketException ex) when (IsEndpointDown(ex))
            {
                EndpointIsDown(ex);
                return;
            }

            Touch();
            _endpointStatistics?.AddBytesWritten(data.Value.Length);

            if (_plugins.HasDataSentHook)
            {
                await _plugins.DataSentAsync(this, NpDataDirection.ClientToEndpoint, data.Value.Length);
            }
        }

        private async Task RelayEndpointToClientAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(NetProxy.Library.Constants.UdpMaxDatagramSize);

            try
            {
                while (_abort.IsCancellationRequested == false)
                {
                    int length;

                    try
                    {
                        length = await _endpointSocket!.ReceiveAsync(buffer, SocketFlags.None, _abort.Token);
                    }
                    catch (SocketException ex) when (IsEndpointDown(ex))
                    {
                        EndpointIsDown(ex);
                        return;
                    }

                    Touch();
                    Interlocked.Add(ref _bytesOut, length);
                    _endpointStatistics?.AddBytesRead(length);

                    var data = await ThroughPluginsAsync(buffer.AsMemory(0, length), NpDataDirection.EndpointToClient);
                    if (data == null)
                    {
                        continue;
                    }

                    await _listener.SendToClientAsync(_client, data.Value, _abort.Token);
                    _proxy.Statistics.AddBytesWritten(data.Value.Length);

                    if (_plugins.HasDataSentHook)
                    {
                        await _plugins.DataSentAsync(this, NpDataDirection.EndpointToClient, data.Value.Length);
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                //The session is over.
            }
            catch (SocketException ex)
            {
                MarkEnded(NpDisconnectReason.Aborted);
                Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} ended: {ex.GetBaseException().Message}");
                Abort();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// The endpoint answered with an ICMP "port unreachable" (nothing is listening there), which a connected UDP socket reports as
        /// an error. The session ends and the endpoint is tried last for a while, so the client's next datagram can reach another one.
        /// </summary>
        private static bool IsEndpointDown(SocketException ex)
            => ex.SocketErrorCode is SocketError.ConnectionRefused or SocketError.ConnectionReset or SocketError.HostUnreachable or SocketError.NetworkUnreachable;

        private void EndpointIsDown(SocketException ex)
        {
            _endpointStatistics?.MarkFailed();
            MarkEnded(NpDisconnectReason.EndpointUnavailable);
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey}: the endpoint is not there ({ex.SocketErrorCode}).");
            Abort();
        }

        //---------------------------------------------------------------- What the console and the plugins see

        public NpConnectionGridItem GetGridItem()
        {
            var listen = _listener.LocalEndPoint;
            var destination = _endpointAddress?.ToString() ?? string.Empty;

            return new NpConnectionGridItem
            {
                Id = Id,
                StartDateTime = StartDateTime,
                LastActivityDateTime = LastActivityDateTime,
                SourceAddress = _clientKey,
                SourceHostName = NpHostNames.Get(_clientKey),
                SourcePort = Key.Port,
                ListenAddress = NpNetwork.Unmap(listen.Address).ToString(),
                ListenPort = listen.Port,
                DestinationAddress = destination,
                DestinationHostName = destination.Length > 0 ? NpHostNames.Get(destination) : null,
                DestinationPort = _endpointInfo?.Port ?? 0,
                Protocol = "Udp",
                Version = string.Empty,
                ClientTls = string.Empty,
                EndpointTls = string.Empty,
                IsConnectedToEndpoint = _endpointInfo != null,
                BytesIn = (ulong)Interlocked.Read(ref _bytesIn),
                BytesOut = (ulong)Interlocked.Read(ref _bytesOut)
            };
        }

        NpProxyInfo INpConnection.Proxy => _proxy.Info;
        DateTime INpConnection.StartedUtc => StartDateTime;
        string INpConnection.ClientAddress => _clientKey;
        int INpConnection.ClientPort => Key.Port;
        string INpConnection.ListenAddress => NpNetwork.Unmap(_listener.LocalEndPoint.Address).ToString();
        int INpConnection.ListenPort => _listener.LocalEndPoint.Port;
        string? INpConnection.HostName => null;
        string? INpConnection.ClientTls => null;
        string? INpConnection.EndpointTls => null;
        bool INpConnection.IsHttp2 => false;
        NpEndpointInfo? INpConnection.Endpoint => _endpointInfo;
        long INpConnection.BytesFromClient => Interlocked.Read(ref _bytesIn);
        long INpConnection.BytesFromEndpoint => Interlocked.Read(ref _bytesOut);
        IDictionary<string, object?> INpConnection.State => _state;

        void INpConnection.Close(string reason)
        {
            _reason = NpDisconnectReason.ClosedByPlugin;
            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"UDP session {Id} from {_clientKey} was closed by a plugin: {reason}");
            Abort();
        }
    }
}
