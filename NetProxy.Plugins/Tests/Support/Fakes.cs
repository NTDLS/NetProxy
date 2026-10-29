using NetProxy.PluginApi;
using System.Text;

namespace NetProxy.Plugins.Tests.Support
{
    internal sealed class FakeConnection : INpConnection
    {
        public Guid Id { get; } = Guid.NewGuid();
        public NpProxyInfo Proxy { get; set; } = new(Guid.NewGuid(), "web", 443, NpTrafficKind.Https);
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public string ClientAddress { get; set; } = "203.0.113.7";
        public int ClientPort { get; set; } = 51234;
        public string ListenAddress { get; set; } = "0.0.0.0";
        public int ListenPort => Proxy.ListenPort;
        public string? HostName { get; set; } = "www.example.com";
        public string? ClientTls { get; set; } = "TLS 1.3";
        public string? EndpointTls { get; set; }
        public bool IsHttp2 { get; set; }
        public NpEndpointInfo? Endpoint { get; set; }
        public long BytesFromClient { get; set; }
        public long BytesFromEndpoint { get; set; }
        public IDictionary<string, object?> State { get; } = new Dictionary<string, object?>();
        public string? ClosedBecause { get; private set; }

        public void Close(string reason) => ClosedBecause = reason;
    }

    internal sealed class FakeHost(Dictionary<string, string>? settings = null, string? dataDirectory = null) : INpPluginHost
    {
        public string InstanceName => "Anti-Hammer";
        public IReadOnlyDictionary<string, string> Settings { get; } = settings ?? new Dictionary<string, string>();
        public string DataDirectory { get; } = dataDirectory ?? Path.Combine(Path.GetTempPath(), "antihammer-" + Guid.NewGuid().ToString("N"));
        public List<(NpPluginLogLevel Level, string Message)> Messages { get; } = [];

        public void Log(NpPluginLogLevel level, string message)
        {
            lock (Messages)
            {
                Messages.Add((level, message));
            }
        }

        public IEnumerable<string> Texts => Messages.Select(o => o.Message);
    }

    /// <summary>
    /// A clock that only moves when it is told to.
    /// </summary>
    internal sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }

    internal static class Http
    {
        public static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

        public static byte[] Request(string target, string method = "GET", string host = "www.example.com", string extraHeaders = "", string body = "")
            => Bytes($"{method} {target} HTTP/1.1\r\nHost: {host}\r\n" + (body.Length > 0 ? $"Content-Length: {body.Length}\r\n" : string.Empty) + extraHeaders + "\r\n" + body);

        public static string Text(ReadOnlyMemory<byte>? bytes) => Encoding.Latin1.GetString(bytes!.Value.Span);
    }
}
