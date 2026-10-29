using NetProxy.PluginApi;
using NetProxy.Plugins.Common.TrafficTracker;
using NetProxy.Plugins.TrafficTracker.Http;
using System.Diagnostics;

namespace NetProxy.Plugins.TrafficTracker
{
    /// <summary>
    /// Follows the requests and the responses of one connection, and turns every request into a <see cref="WebRequestRecord"/>. A keep-alive
    /// connection carries many requests, and a response answers the oldest request that has not been answered, so the requests wait in a
    /// queue for their responses.
    /// </summary>
    internal sealed class ConnectionTracker
    {
        public const string StateKey = "NetProxy.TrafficTracker";

        /// <summary>A client that pipelines more requests than this without being answered is not followed any more.</summary>
        private const int MaxPending = 64;

        private readonly object _lock = new();
        private readonly TrackerSettings _settings;
        private readonly Action<WebRequestRecord> _emit;
        private readonly INpConnection _connection;
        private readonly Http1StreamParser _requests;
        private readonly Http1StreamParser _responses;
        private readonly Queue<Pending> _pending = new();

        private sealed record Pending(WebRequestRecord Record, long ReceivedTimestamp, bool IsHead);

        public ConnectionTracker(INpConnection connection, TrackerSettings settings, Action<WebRequestRecord> emit)
        {
            _connection = connection;
            _settings = settings;
            _emit = emit;

            _requests = new Http1StreamParser(isRequest: true, OnRequest);
            _responses = new Http1StreamParser(isRequest: false, OnResponse);
        }

        /// <summary>True when neither direction is being followed, so that the connection can be left alone.</summary>
        public bool IsFinished => _requests.IsStopped && _responses.IsStopped;

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
        /// The connection ended: the requests that were never answered are kept as they are.
        /// </summary>
        public void Finish()
        {
            lock (_lock)
            {
                while (_pending.TryDequeue(out var pending))
                {
                    _emit(pending.Record);
                }
            }
        }

        private bool OnRequest(HttpHead head)
        {
            var record = Build(head);
            if (record == null)
            {
                return false;
            }

            if (_settings.RecordResponses == false)
            {
                _emit(record);
                return false;
            }

            _pending.Enqueue(new Pending(record, Stopwatch.GetTimestamp(), head.Method == "HEAD"));

            //A client that sends and sends without being answered is not worth holding the memory for.
            while (_pending.Count > MaxPending)
            {
                _emit(_pending.Dequeue().Record);
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

            var pending = _pending.Dequeue();
            var record = pending.Record;

            record.StatusCode = status;
            record.DurationMs = (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(pending.ReceivedTimestamp).TotalMilliseconds);
            record.ResponseBytes = head.ContentLength;
            record.ContentType = MediaType(head.Header("Content-Type"));
            _emit(record);

            //The response to a HEAD request has the headers of the body it does not have.
            return pending.IsHead;
        }

        private WebRequestRecord? Build(HttpHead head)
        {
            var (path, query, targetHost) = SplitTarget(head.Target);

            var host = NormalizeHost(head.Header("Host")) ?? targetHost ?? NormalizeHost(_connection.HostName) ?? string.Empty;
            var client = _connection.ClientAddress;

            if (_settings.TrustForwardedFor && head.Header("X-Forwarded-For") is { Length: > 0 } forwarded)
            {
                var first = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (string.IsNullOrEmpty(first) == false)
                {
                    client = first;
                }
            }

            if (_settings.IsIgnoredHost(host) || _settings.IsIgnoredPath(path) || _settings.IsIgnoredClient(client))
            {
                return null;
            }

            return new WebRequestRecord
            {
                ObservedUtc = DateTime.UtcNow,
                Proxy = _connection.Proxy.Name,
                ListenPort = _connection.Proxy.ListenPort,
                IsTls = _connection.ClientTls != null,
                Host = host,
                Method = Limit(head.Method, 32),
                Path = Limit(path, 2048),
                Query = _settings.RecordQueryStrings ? LimitOrNull(query, 2048) : null,
                HttpVersion = head.Version.EndsWith("1.0", StringComparison.Ordinal) ? "1.0" : "1.1",
                ClientAddress = Limit(client, 45),
                UserAgent = LimitOrNull(head.Header("User-Agent"), 1024),
                Referrer = _settings.RecordReferrers ? CleanReferrer(head.Header("Referer")) : null,
                RequestBytes = head.ContentLength,
                ConnectionId = _connection.Id
            };
        }

        private string? CleanReferrer(string? referrer)
        {
            if (string.IsNullOrWhiteSpace(referrer))
            {
                return null;
            }

            if (_settings.RecordQueryStrings == false)
            {
                var cut = referrer.IndexOfAny(['?', '#']);
                if (cut >= 0)
                {
                    referrer = referrer[..cut];
                }
            }

            return Limit(referrer, 2048);
        }

        /// <summary>
        /// Splits the target of a request: "/path?query", or "http://host/path?query" when a client talks to the proxy as to a proxy.
        /// </summary>
        internal static (string Path, string? Query, string? Host) SplitTarget(string target)
        {
            string? host = null;

            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var start = target.IndexOf("://", StringComparison.Ordinal) + 3;
                var slash = target.IndexOfAny(['/', '?'], start);
                host = NormalizeHost(slash < 0 ? target[start..] : target[start..slash]);
                target = slash < 0 ? "/" : target[slash..];
            }

            var hash = target.IndexOf('#');
            if (hash >= 0)
            {
                target = target[..hash];
            }

            var question = target.IndexOf('?');
            var path = question < 0 ? target : target[..question];
            var query = question < 0 || question == target.Length - 1 ? null : target[(question + 1)..];

            return (path.Length == 0 ? "/" : path, query, host);
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
            return host.Length == 0 ? null : Limit(host.ToLowerInvariant(), 253);
        }

        private static string? MediaType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return null;
            }

            var semicolon = contentType.IndexOf(';');
            return Limit((semicolon < 0 ? contentType : contentType[..semicolon]).Trim().ToLowerInvariant(), 127);
        }

        private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];

        private static string? LimitOrNull(string? value, int length) => value == null ? null : Limit(value, length);
    }
}
