using NetProxy.Library.Utilities;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Receives the datagrams on one UDP socket of a proxy. UDP has no connections, so each client (address and port) gets a session that
    /// is kept for as long as it keeps sending (see <see cref="NpUdpSession"/>). A UDP port belongs to one proxy: a datagram does not say
    /// which host it is for, so proxies cannot share it the way TCP proxies share a port by host name.
    /// </summary>
    internal sealed class NpUdpListener
    {
        /// <summary>
        /// A client that was refused (a plugin said no, or no endpoint worked) is ignored for this long, so that it does not cost a
        /// session, an event and a lookup for every datagram it sends.
        /// </summary>
        private static readonly TimeSpan _refusedFor = TimeSpan.FromSeconds(5);

        /// <summary>
        /// A client that was turned away at a limit is ignored for this long, which is enough to not tell the plugins about every datagram.
        /// </summary>
        private static readonly TimeSpan _limitRefusedFor = TimeSpan.FromSeconds(1);

        private readonly NpProxy _proxy;
        private readonly Socket _socket;
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<IPEndPoint, NpUdpSession> _sessions = new();
        private readonly ConcurrentDictionary<string, long> _refused = new();
        private Task? _receiveLoop;
        private Timer? _sweep;

        private NpUdpListener(NpProxy proxy, Socket socket)
        {
            _proxy = proxy;
            _socket = socket;
        }

        public CancellationToken StopToken => _stop.Token;

        /// <summary>
        /// The local address and port, as the clients of the sessions connected to it.
        /// </summary>
        public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

        public string Description
        {
            get
            {
                var endPoint = LocalEndPoint;
                var any = endPoint.Address.Equals(IPAddress.Any) || endPoint.Address.Equals(IPAddress.IPv6Any);
                var family = _socket.AddressFamily == AddressFamily.InterNetworkV6 && _socket.DualMode
                    ? "IPv4 and IPv6" : (endPoint.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4");
                return (any ? $"UDP port {endPoint.Port} ({family})" : $"UDP {NpNetwork.FormatHostPort(endPoint.Address.ToString(), endPoint.Port)}");
            }
        }

        /// <summary>
        /// Binds the UDP sockets of the proxy and starts receiving. Throws, with nothing left open, if an address cannot be bound.
        /// </summary>
        public static List<NpUdpListener> StartAll(NpProxy proxy)
        {
            var sockets = NpNetwork.CreateUdpSockets(proxy.Configuration);
            var listeners = sockets.Select(o => new NpUdpListener(proxy, o)).ToList();

            foreach (var listener in listeners)
            {
                listener.Start();
            }

            return listeners;
        }

        private void Start()
        {
            _receiveLoop = Task.Run(ReceiveLoopAsync);
            _sweep = new Timer(_ => Sweep(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Stops receiving, ends the sessions and closes the socket.
        /// </summary>
        public void Stop()
        {
            try { _stop.Cancel(); } catch (ObjectDisposedException) { }
            _sweep?.Dispose();

            foreach (var session in _sessions.Values)
            {
                session.Abort();
            }

            try { _socket.Dispose(); } catch { }

            try
            {
                _receiveLoop?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                //The loop ends with an exception when the socket is closed. That is how it is meant to end.
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[NetProxy.Library.Constants.UdpMaxDatagramSize];
            EndPoint any = _socket.AddressFamily == AddressFamily.InterNetworkV6 ? new IPEndPoint(IPAddress.IPv6Any, 0) : new IPEndPoint(IPAddress.Any, 0);

            while (_stop.IsCancellationRequested == false)
            {
                SocketReceiveFromResult received;

                try
                {
                    received = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, _stop.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
                {
                    break; //The listener was stopped.
                }
                catch (SocketException ex) when (_stop.IsCancellationRequested || ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.MessageSize or SocketError.NetworkReset)
                {
                    continue; //About a client that is gone, or a datagram that did not fit. Not about the socket.
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Proxy '{_proxy.Configuration.Name}' failed to receive a datagram: {ex.Message}");
                    await Task.Delay(100, CancellationToken.None);
                    continue;
                }

                try
                {
                    Dispatch((IPEndPoint)received.RemoteEndPoint, buffer, received.ReceivedBytes);
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write($"Proxy '{_proxy.Configuration.Name}' failed to handle a datagram", ex);
                }
            }
        }

        /// <summary>
        /// Gives a datagram to the session of its client, starting the session if the client has none.
        /// </summary>
        private void Dispatch(IPEndPoint client, byte[] buffer, int length)
        {
            var key = new IPEndPoint(NpNetwork.Unmap(client.Address), client.Port);

            if (_sessions.TryGetValue(key, out var existing) && existing.Enqueue(buffer, length) != NpUdpSession.EnqueueResult.Ended)
            {
                return;
            }

            var clientKey = key.Address.ToString();

            if (_refused.TryGetValue(clientKey, out var until) && DateTime.UtcNow.Ticks < until)
            {
                return;
            }

            if (_proxy.IsRunning == false)
            {
                return;
            }

            if (_proxy.TryAdmit(clientKey) == false)
            {
                Refuse(clientKey, _limitRefusedFor);
                _ = _proxy.Plugins.ConnectionRefusedAsync(_proxy.Info, clientKey, "The proxy is at one of its connection limits.").AsTask();
                return;
            }

            var session = new NpUdpSession(this, _proxy, client, key, clientKey);
            _sessions[key] = session;
            session.Enqueue(buffer, length);
            session.Start();
        }

        /// <summary>
        /// The client is ignored for a few seconds.
        /// </summary>
        internal void Refuse(string clientKey, TimeSpan? duration = null)
            => _refused[clientKey] = DateTime.UtcNow.Add(duration ?? _refusedFor).Ticks;

        internal void Remove(NpUdpSession session)
            => _sessions.TryRemove(new KeyValuePair<IPEndPoint, NpUdpSession>(session.Key, session));

        internal async ValueTask SendToClientAsync(IPEndPoint client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
            => await _socket.SendToAsync(data, SocketFlags.None, client, cancellationToken);

        /// <summary>
        /// Ends the sessions that have not had a datagram, either way, for the idle timeout of the proxy.
        /// </summary>
        private void Sweep()
        {
            var seconds = _proxy.Configuration.IdleTimeoutSeconds > 0
                ? _proxy.Configuration.IdleTimeoutSeconds : NetProxy.Library.Constants.DefaultUdpIdleTimeoutSeconds;
            var limit = TimeSpan.FromSeconds(seconds);

            foreach (var session in _sessions.Values)
            {
                if (DateTime.UtcNow - session.LastActivityDateTime >= limit)
                {
                    _ = session.ExpireAsync(seconds);
                }
            }

            var now = DateTime.UtcNow.Ticks;
            foreach (var entry in _refused.Where(o => o.Value <= now))
            {
                _refused.TryRemove(entry.Key, out _);
            }
        }
    }
}
