using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetProxy.Tests.Support
{
    /// <summary>
    /// Sends back whatever it is sent.
    /// </summary>
    public sealed class TcpEchoBackend : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();

        public int Port { get; }

        public TcpEchoBackend()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(Accept);
        }

        private async Task Accept()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        {
                            try
                            {
                                var stream = client.GetStream();
                                var buffer = new byte[64 * 1024];
                                int read;
                                while ((read = await stream.ReadAsync(buffer, _stop.Token)) > 0)
                                {
                                    await stream.WriteAsync(buffer.AsMemory(0, read), _stop.Token);
                                }
                            }
                            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                            {
                            }
                        }
                    });
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }

    /// <summary>
    /// A web server that speaks TLS itself, with a certificate of its own.
    /// </summary>
    public sealed class TlsEchoBackend : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly X509Certificate2 _certificate;
        private readonly CancellationTokenSource _stop = new();

        public int Port { get; }

        public TlsEchoBackend(byte[] pfx)
        {
            _certificate = X509CertificateLoader.LoadPkcs12(pfx, Certs.Password);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(Accept);
        }

        private async Task Accept()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        {
                            try
                            {
                                using var ssl = new SslStream(client.GetStream());
                                await ssl.AuthenticateAsServerAsync(_certificate);

                                var request = new StringBuilder();
                                var buffer = new byte[4096];
                                while (request.ToString().Contains("\r\n\r\n") == false)
                                {
                                    var read = await ssl.ReadAsync(buffer, _stop.Token);
                                    if (read <= 0)
                                    {
                                        return;
                                    }

                                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                                }

                                const string body = "hello from tls end";
                                await ssl.WriteAsync(Encoding.ASCII.GetBytes(
                                    $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}"), _stop.Token);
                            }
                            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException
                                or System.Security.Authentication.AuthenticationException)
                            {
                            }
                        }
                    });
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _certificate.Dispose();
        }
    }
}

namespace NetProxy.Tests.Support
{
    /// <summary>
    /// A UDP server that answers every datagram with the same bytes, behind an optional tag, and remembers who sent them.
    /// </summary>
    public sealed class UdpEchoBackend : IDisposable
    {
        private readonly System.Net.Sockets.Socket _socket;
        private readonly CancellationTokenSource _stop = new();
        private readonly string _tag;
        private readonly object _lock = new();
        private readonly List<System.Net.IPEndPoint> _sources = [];
        private int _received;

        public int Port { get; }

        /// <summary>The client address and port of every datagram, in the order they arrived (as the endpoint sees them: the proxy).</summary>
        public List<System.Net.IPEndPoint> Sources { get { lock (_lock) { return [.. _sources]; } } }

        public int Received => Volatile.Read(ref _received);

        public UdpEchoBackend(string tag = "")
        {
            _tag = tag;
            _socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
            _socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            Port = ((System.Net.IPEndPoint)_socket.LocalEndPoint!).Port;
            _ = Task.Run(Serve);
        }

        private async Task Serve()
        {
            var buffer = new byte[65535];
            System.Net.EndPoint any = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);

            try
            {
                while (true)
                {
                    var result = await _socket.ReceiveFromAsync(buffer, System.Net.Sockets.SocketFlags.None, any, _stop.Token);
                    lock (_lock)
                    {
                        _sources.Add((System.Net.IPEndPoint)result.RemoteEndPoint);
                    }
                    Interlocked.Increment(ref _received);

                    var reply = System.Text.Encoding.ASCII.GetBytes(_tag).Concat(buffer.Take(result.ReceivedBytes)).ToArray();
                    await _socket.SendToAsync(reply, System.Net.Sockets.SocketFlags.None, result.RemoteEndPoint, _stop.Token);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or System.Net.Sockets.SocketException)
            {
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _socket.Dispose();
        }
    }

    /// <summary>
    /// A UDP client with its own port, for sending datagrams to a proxy and waiting for what comes back.
    /// </summary>
    public sealed class UdpTestClient : IDisposable
    {
        private readonly System.Net.Sockets.UdpClient _client;
        private readonly System.Net.IPEndPoint _server;

        public int LocalPort => ((System.Net.IPEndPoint)_client.Client.LocalEndPoint!).Port;

        public UdpTestClient(int port, System.Net.IPAddress? address = null)
        {
            address ??= System.Net.IPAddress.Loopback;
            _server = new System.Net.IPEndPoint(address, port);
            _client = new System.Net.Sockets.UdpClient(address.AddressFamily);
            _client.Client.Bind(new System.Net.IPEndPoint(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? System.Net.IPAddress.IPv6Loopback : System.Net.IPAddress.Loopback, 0));
        }

        public async Task Send(byte[] data) => await _client.SendAsync(data, data.Length, _server);

        public Task Send(string text) => Send(System.Text.Encoding.ASCII.GetBytes(text));

        /// <summary>
        /// The next datagram, or null if none comes in the time.
        /// </summary>
        public async Task<byte[]?> Receive(double seconds = 3)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));

            try
            {
                return (await _client.ReceiveAsync(timeout.Token)).Buffer;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return null;
            }
        }

        public async Task<string?> Receive(string _, double seconds = 3)
            => await Receive(seconds) is { } data ? System.Text.Encoding.ASCII.GetString(data) : null;

        /// <summary>
        /// Sends a datagram and returns what comes back for it, or null.
        /// </summary>
        public async Task<string?> Ask(string text, double seconds = 3)
        {
            await Send(text);
            return await Receive(string.Empty, seconds);
        }

        public void Dispose() => _client.Dispose();
    }
}
