using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Plugins.Metrics
{
    /// <summary>
    /// A very small web server that answers one address (/metrics) with the metrics. It uses a socket of its own, not a proxy, so it needs
    /// nothing from the machine (no URL reservation), and it answers one request per connection.
    /// </summary>
    internal sealed class MetricsServer : IDisposable
    {
        private const int MaxConnections = 16;

        private readonly IPEndPoint _endpoint;
        private readonly string _path;
        private readonly string? _token;
        private readonly Func<string> _render;
        private readonly Action<string> _warn;
        private readonly CancellationTokenSource _stop = new();
        private TcpListener? _listener;
        private int _active;

        public MetricsServer(IPEndPoint endpoint, string path, string? token, Func<string> render, Action<string> warn)
        {
            _endpoint = endpoint;
            _path = path;
            _token = token;
            _render = render;
            _warn = warn;
        }

        /// <summary>The port it listens on (the one that was asked for, or the one that was chosen when that was 0).</summary>
        public int Port => ((IPEndPoint?)_listener?.LocalEndpoint)?.Port ?? _endpoint.Port;

        public void Start()
        {
            _listener = new TcpListener(_endpoint);
            _listener.Start(32);
            _ = Task.Run(AcceptAsync);
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (_stop.IsCancellationRequested == false)
                {
                    var client = await _listener!.AcceptTcpClientAsync(_stop.Token);

                    if (Interlocked.Increment(ref _active) > MaxConnections)
                    {
                        Interlocked.Decrement(ref _active);
                        client.Dispose();
                        continue;
                    }

                    _ = Task.Run(() => ServeAsync(client));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (_stop.IsCancellationRequested == false)
            {
                _warn($"The metrics server stopped: {ex.GetBaseException().Message}");
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));

                    var stream = client.GetStream();
                    var head = await ReadHeadAsync(stream, timeout.Token);
                    if (head == null)
                    {
                        return;
                    }

                    var lines = head.Split("\r\n");
                    var start = lines[0].Split(' ');

                    byte[] response;
                    if (start.Length < 2 || start[0] is not ("GET" or "HEAD"))
                    {
                        response = Response(405, "Method Not Allowed", "Only GET.\n", includeBody: true);
                    }
                    else if (_token != null && Authorized(lines) == false)
                    {
                        response = Response(401, "Unauthorized", "Send the token as 'Authorization: Bearer <token>'.\n", includeBody: true, "WWW-Authenticate: Bearer\r\n");
                    }
                    else if (start[1].Split('?')[0] != _path)
                    {
                        response = Response(404, "Not Found", $"The metrics are at {_path}\n", includeBody: true);
                    }
                    else
                    {
                        response = Response(200, "OK", _render(), includeBody: start[0] == "GET", contentType: "text/plain; version=0.0.4; charset=utf-8");
                    }

                    await stream.WriteAsync(response, timeout.Token);
                    await stream.FlushAsync(timeout.Token);
                    client.Client.Shutdown(SocketShutdown.Send);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                //A client that went away, or was too slow.
            }
            catch (Exception ex)
            {
                _warn($"A request for the metrics failed: {ex.GetBaseException().Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private static async Task<string?> ReadHeadAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var length = 0;

            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
                if (read == 0)
                {
                    return null;
                }

                length += read;
                var text = Encoding.Latin1.GetString(buffer, 0, length);
                var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end >= 0)
                {
                    return text[..end];
                }
            }

            return null;
        }

        private bool Authorized(string[] lines)
        {
            var header = lines.Skip(1).FirstOrDefault(o => o.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase));
            var value = header?[(header.IndexOf(':') + 1)..].Trim();

            if (value == null || value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            //Compared in constant time, so that how long it takes does not give the token away.
            return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(value[7..].Trim())), SHA256.HashData(Encoding.UTF8.GetBytes(_token!)));
        }

        private static byte[] Response(int status, string reason, string body, bool includeBody, string extra = "", string contentType = "text/plain; charset=utf-8")
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\n{extra}Cache-Control: no-store\r\nConnection: close\r\n\r\n";
            return [.. Encoding.ASCII.GetBytes(head), .. (includeBody ? bytes : [])];
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener?.Stop();
            _stop.Dispose();
        }
    }
}
