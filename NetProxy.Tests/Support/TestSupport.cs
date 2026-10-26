using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Proxy;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetProxy.Tests.Support
{
    /// <summary>
    /// The tests keep their configuration under their own name, never in the one of an installed NetProxy. This has to be set
    /// before the service code first looks at it, which is why it runs when the assembly is loaded.
    /// </summary>
    internal static class TestEnvironment
    {
        public const string ConfigurationName = "NetProxyTests";

        [ModuleInitializer]
        internal static void Initialize()
            => Environment.SetEnvironmentVariable("NETPROXY_CONFIG_NAME", ConfigurationName);

        /// <summary>
        /// The folder that the service keeps the configuration of the tests in.
        /// </summary>
        public static string ConfigurationFolder => Path.Combine(
            OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) : "/usr/share",
            ConfigurationName);

        public static void ResetConfiguration()
        {
            try { Directory.Delete(ConfigurationFolder, recursive: true); } catch (DirectoryNotFoundException) { }
        }
    }

    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nptest" + Guid.NewGuid().ToString("N")[..8]);

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }

    public static class Net
    {
        /// <summary>
        /// A UDP port that nothing has bound, for the moment.
        /// </summary>
        public static int FreePortForTcpAndUdp()
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var port = FreePort();

                try
                {
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    return port;
                }
                catch (SocketException)
                {
                }
            }

            throw new InvalidOperationException("No port is free for both TCP and UDP.");
        }

        /// <summary>
        /// A UDP port that nothing has bound, for the moment.
        /// </summary>
        public static int FreeUdpPort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        /// <summary>
        /// A TCP port that nothing is listening on, for the moment.
        /// </summary>
        public static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    public static class Certs
    {
        public const string Password = "pw";

        public static byte[] PfxBytes(string commonName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(commonName);
            request.CertificateExtensions.Add(names.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
            return certificate.Export(X509ContentType.Pfx, Password);
        }

        /// <summary>
        /// Sets the PFX file up as a certificate of the service, which is what a proxy refers to, and returns its name. The same file
        /// always gets the same name, so registering it again replaces what was read from it.
        /// </summary>
        public static string Register(string path, string password = Password)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty; //No certificate.
            }

            var name = "t-" + System.IO.Path.GetFileNameWithoutExtension(path) + "-"
                + Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..8];

            var problem = NetProxy.Service.Singletons.Certificates.Upsert(new NetProxy.Library.Payloads.Routing.NpCertificateConfiguration
            {
                Id = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(name))),
                Name = name,
                Path = path,
                Password = password
            }, out _, out _);

            if (problem != null)
            {
                throw new InvalidOperationException(problem);
            }

            return name;
        }

        public static string WritePfx(TempDirectory directory, string commonName, string? fileName = null)
        {
            var path = directory.File(fileName ?? commonName + ".pfx");
            File.WriteAllBytes(path, PfxBytes(commonName));
            return path;
        }
    }

    /// <summary>
    /// A small web server that says what it was asked: which headers it got and which backend it is.
    /// </summary>
    public sealed class EchoBackend : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _name;

        public int Port { get; }

        public EchoBackend(string name)
        {
            _name = name;
            Port = Net.FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(Serve);
        }

        private async Task Serve()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var body = Encoding.ASCII.GetBytes(
                        $"backend={_name} host=[{context.Request.Headers["Host"]}] xff=[{context.Request.Headers["X-Forwarded-For"]}]"
                        + $" proto=[{context.Request.Headers["X-Forwarded-Proto"]}] path=[{context.Request.RawUrl}] end");
                    context.Response.OutputStream.Write(body);
                    context.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); _listener.Close(); } catch { }
        }
    }

    public static class ProxyBuilder
    {
        /// <summary>
        /// A proxy on the loopback address in front of one endpoint.
        /// </summary>
        public static NpProxyConfiguration Configuration(string name, int port, TrafficType type, int endpointPort, Action<NpProxyConfiguration>? change = null)
        {
            var configuration = new NpProxyConfiguration
            {
                Name = name,
                ListenPort = port,
                TrafficType = type,
                ListenOnAllAddresses = false,
                AutoStart = true
            };

            configuration.Bindings.Add(new NpBinding { Enabled = true, Address = "127.0.0.1" });
            configuration.Endpoints.Add(new NpEndpoint { Enabled = true, Address = "127.0.0.1", Port = endpointPort, TrafficType = TrafficType.Http });
            change?.Invoke(configuration);
            return configuration;
        }

        /// <summary>
        /// An endpoint on the loopback address (switched on, as every new endpoint is).
        /// </summary>
        public static NpEndpoint Endpoint(int port)
            => new() { Address = "127.0.0.1", Port = port, TrafficType = TrafficType.Http };

        public static NpProxy Start(NpProxyConfiguration configuration)
        {
            var proxy = new NpProxy(configuration);
            if (proxy.Start(out var error) == false)
            {
                throw new InvalidOperationException($"The proxy '{configuration.Name}' did not start: {error}");
            }

            return proxy;
        }
    }

    /// <summary>
    /// Talks to a proxy the way a client does, with the bytes written out.
    /// </summary>
    public static class Client
    {
        public static async Task<string> Http(int port, string host = "x", string extraHeaders = "", string path = "/")
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {path} HTTP/1.1\r\nHost: {host}\r\n{extraHeaders}Connection: close\r\n\r\n"));

            var text = new StringBuilder();
            var buffer = new byte[8192];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, timeout.Token);
                    if (read <= 0)
                    {
                        break;
                    }

                    text.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                //The proxy closed the connection.
            }

            return text.ToString();
        }

        /// <summary>
        /// A request over TLS. The name that is asked for in the handshake is <paramref name="serverName"/>. Returns the response and
        /// the name of the certificate that was presented. Throws if the handshake fails.
        /// </summary>
        public static async Task<(string Body, string CertificateName)> Https(int port, string serverName, string host = "x", string extraHeaders = "")
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(serverName);

            var certificateName = new X509Certificate2(ssl.RemoteCertificate!).GetNameInfo(X509NameType.SimpleName, false);
            await ssl.WriteAsync(Encoding.ASCII.GetBytes($"GET / HTTP/1.1\r\nHost: {host}\r\n{extraHeaders}\r\n"));

            var text = new StringBuilder();
            var buffer = new byte[4096];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                while (text.ToString().Contains(" end") == false)
                {
                    var read = await ssl.ReadAsync(buffer, timeout.Token);
                    if (read <= 0)
                    {
                        break;
                    }

                    text.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                //The proxy closed the connection.
            }

            return (text.ToString(), certificateName);
        }

        /// <summary>
        /// Reads exactly this many bytes (failing if they do not come in five seconds), for when the bytes themselves do not matter.
        /// </summary>
        public static async Task ReadExactly(Stream stream, int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await stream.ReadExactlyAsync(new byte[count], timeout.Token);
        }

        /// <summary>
        /// Waits until what the check returns is what is wanted, for up to <paramref name="seconds"/>. Returns the last thing it saw.
        /// </summary>
        public static async Task<T> Eventually<T>(Func<Task<T>> check, Func<T, bool> wanted, double seconds = 15)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            T value = await check();

            while (wanted(value) == false && stopwatch.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                await Task.Delay(300);
                value = await check();
            }

            return value;
        }
    }
}
