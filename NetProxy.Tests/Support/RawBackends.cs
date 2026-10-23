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
