using NetProxy.PluginApi;
using System.Diagnostics;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// One request and what it was answered with.
    /// </summary>
    internal sealed class HttpExchange
    {
        public DateTime StartedUtc { get; init; }
        public string Proxy { get; init; } = string.Empty;
        public int ListenPort { get; init; }
        public bool IsTls { get; init; }
        public string Host { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string Path { get; init; } = "/";
        public string Query { get; init; } = string.Empty;
        public string HttpVersion { get; init; } = "1.1";
        public string Client { get; init; } = string.Empty;
        public string? UserAgent { get; init; }
        public string? Referrer { get; init; }
        public long? RequestBytes { get; init; }
        public Guid ConnectionId { get; init; }
        public HttpHead Request { get; init; } = null!;

        /// <summary>0 when the connection ended before the request was answered.</summary>
        public int Status { get; set; }
        public long? ResponseBytes { get; set; }
        public string? ContentType { get; set; }
        public int DurationMs { get; set; }
    }

    /// <summary>
    /// Follows the requests and the responses of one HTTP/1.x connection and turns every request into a <see cref="HttpExchange"/>. A
    /// keep-alive connection carries many requests, and a response answers the oldest request that has not been answered, so the
    /// requests wait in a queue for their responses.
    /// </summary>
    internal sealed class ExchangeTracker
    {
        /// <summary>A client that pipelines more requests than this without being answered is not followed any more.</summary>
        private const int MaxPending = 64;

        private readonly object _lock = new();
        private readonly INpConnection _connection;
        private readonly Func<HttpHead, string> _client;
        private readonly Action<HttpExchange> _emit;
        private readonly Http1StreamParser _requests;
        private readonly Http1StreamParser _responses;
        private readonly Queue<(HttpExchange Exchange, long Timestamp, bool IsHead)> _pending = new();

        /// <param name="client">Says who the client of a request is (see <see cref="TrustedProxies"/>).</param>
        public ExchangeTracker(INpConnection connection, Func<HttpHead, string> client, Action<HttpExchange> emit)
        {
            _connection = connection;
            _client = client;
            _emit = emit;

            _requests = new Http1StreamParser(isRequest: true, OnRequest);
            _responses = new Http1StreamParser(isRequest: false, OnResponse);
        }

        public void Feed(NpDataDirection direction, ReadOnlySpan<byte> data)
        {
            lock (_lock)
            {
                if (direction == NpDataDirection.ClientToEndpoint)
                {
                    _requests.Feed(data);
                }
                else
                {
                    _responses.Feed(data);
                }
            }
        }

        /// <summary>
        /// The connection ended: the requests that were never answered are reported as they are.
        /// </summary>
        public void Finish()
        {
            lock (_lock)
            {
                while (_pending.TryDequeue(out var pending))
                {
                    _emit(pending.Exchange);
                }
            }
        }

        private bool OnRequest(HttpHead head)
        {
            var (path, query) = Split(head.Target);

            var exchange = new HttpExchange
            {
                StartedUtc = DateTime.UtcNow,
                Proxy = _connection.Proxy.Name,
                ListenPort = _connection.Proxy.ListenPort,
                IsTls = _connection.ClientTls != null,
                Host = NormalizeHost(head.Header("Host")) ?? NormalizeHost(_connection.HostName) ?? string.Empty,
                Method = head.Method,
                Path = path,
                Query = query,
                HttpVersion = head.Version.EndsWith("1.0", StringComparison.Ordinal) ? "1.0" : "1.1",
                Client = _client(head),
                UserAgent = head.Header("User-Agent"),
                Referrer = head.Header("Referer"),
                RequestBytes = head.ContentLength,
                ConnectionId = _connection.Id,
                Request = head
            };

            _pending.Enqueue((exchange, Stopwatch.GetTimestamp(), head.Method == "HEAD"));

            while (_pending.Count > MaxPending)
            {
                _emit(_pending.Dequeue().Exchange);
            }

            return false;
        }

        private bool OnResponse(HttpHead head)
        {
            var status = head.StatusCode;

            //An interim response (100 Continue) is not the answer, and neither does it end anything.
            if (status is >= 100 and < 200 && status != 101)
            {
                return false;
            }

            if (_pending.Count == 0)
            {
                return false; //An answer to a request that was not seen (the plug-in was loaded in the middle of the connection).
            }

            var (exchange, timestamp, isHead) = _pending.Dequeue();

            exchange.Status = status;
            exchange.DurationMs = (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            exchange.ResponseBytes = head.ContentLength;
            exchange.ContentType = head.Header("Content-Type");
            _emit(exchange);

            //The response to a HEAD request has the headers of the body it does not have.
            return isHead;
        }

        /// <summary>
        /// Splits the target of a request: "/path?query", or "http://host/path?query" when a client talks to the proxy as to a proxy.
        /// </summary>
        internal static (string Path, string Query) Split(string target)
        {
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var slash = target.IndexOfAny(['/', '?'], target.IndexOf("://", StringComparison.Ordinal) + 3);
                target = slash < 0 ? "/" : target[slash..];
            }

            var hash = target.IndexOf('#');
            if (hash >= 0)
            {
                target = target[..hash];
            }

            var question = target.IndexOf('?');
            var path = question < 0 ? target : target[..question];
            var query = question < 0 || question == target.Length - 1 ? string.Empty : target[question..];

            return (path.Length == 0 ? "/" : path, query);
        }

        /// <summary>
        /// A host in lower case, without the port.
        /// </summary>
        internal static string? NormalizeHost(string? host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            host = host.Trim();

            if (host.StartsWith('['))
            {
                var close = host.IndexOf(']');
                return close > 0 ? host[..(close + 1)].ToLowerInvariant() : host.ToLowerInvariant(); //An IPv6 address.
            }

            var colon = host.LastIndexOf(':');
            if (colon >= 0 && host.IndexOf(':') == colon)
            {
                host = host[..colon];
            }

            host = host.TrimEnd('.');
            return host.Length == 0 ? null : host.ToLowerInvariant();
        }
    }
}
