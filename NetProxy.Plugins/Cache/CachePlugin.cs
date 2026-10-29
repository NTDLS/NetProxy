using NetProxy.PluginApi;
using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.Cache
{
    /// <summary>
    /// Keeps the responses to GET requests in memory and answers the next client for the same URL itself, so that the endpoint is asked once
    /// and not for every client. It is for pages and files that are the same for everyone (a site's front page, images, scripts, downloads).
    ///
    /// It is careful, because what is kept for one client is given to the next: a request with a cookie or an Authorization header, and a
    /// response that sets a cookie, says it is private or no-store, or varies on anything but the encoding, is never kept. A response is
    /// kept for as long as it says (max-age, s-maxage) or for DefaultTtlSeconds if it does not say. Only responses with a Content-Length
    /// are kept (a chunked response is not), and a response bigger than MaxObjectKB is not.
    ///
    /// It works on HTTP/1.x (HTTP/2 is compressed, and is not followed) and only on a connection where the requests and the responses can
    /// be told apart: a client that sends the next request before the last was answered gets no cached answers. A response from the cache
    /// has an Age and an X-Cache: HIT header.
    /// </summary>
    [NpPlugin("Response Cache", Version = "1.0", Author = "NetworkDLS",
        Description = "Keeps responses to GET requests in memory and answers repeat requests itself, so the endpoint is asked once.")]
    public sealed class CachePlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.Cache";
        private static readonly string[] NotKept = ["Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Age"];

        private sealed class Capture(string key)
        {
            private readonly MemoryStream _buffer = new();

            public string Key { get; } = key;
            public bool Failed { get; private set; }
            public HttpResponseHead? Head { get; private set; }
            public byte[] Body { get; private set; } = [];

            /// <summary>Takes what the endpoint sent. Returns true when the whole response is here.</summary>
            public bool Feed(ReadOnlySpan<byte> data, int maxBytes)
            {
                if (Failed)
                {
                    return false;
                }

                _buffer.Write(data);
                var bytes = _buffer.GetBuffer().AsSpan(0, (int)_buffer.Length);

                if (Head == null)
                {
                    var end = HttpResponseHead.FindEnd(bytes);
                    if (end < 0)
                    {
                        Failed = bytes.Length > 32 * 1024;
                        return false;
                    }

                    var head = HttpResponseHead.TryParse(bytes[..end]);
                    if (head == null || head.Status < 200 || head.ContentLength is not { } length || length > maxBytes)
                    {
                        Failed = true;
                        return false;
                    }

                    Head = head;
                    _headLength = end + 4;
                    _bodyLength = (int)length;
                }

                if (bytes.Length < _headLength + _bodyLength)
                {
                    return false;
                }

                Body = bytes.Slice(_headLength, _bodyLength).ToArray();
                return true;
            }

            private int _headLength, _bodyLength;
        }

        private sealed class CacheConnection
        {
            public readonly object Lock = new();
            public readonly RequestReader Reader = new(keepBuffer: true);
            public readonly Queue<string> Methods = new();
            public Http1StreamParser Responses = null!;
            public Capture? Capture;
            public int Outstanding;
            public bool Disabled;
        }

        private readonly TimeProvider _clock;
        private CachePolicy _policy = new();
        private ResponseCache _cache;
        private long _lastReportedHits;
        private int _warnedHttp2;

        public CachePlugin() : this(TimeProvider.System)
        {
        }

        internal CachePlugin(TimeProvider clock)
        {
            _clock = clock;
            _cache = new ResponseCache(64L * 1024 * 1024, 50_000, clock);
        }

        public override TimeSpan? TickInterval => TimeSpan.FromSeconds(30);

        internal ResponseCache Cache => _cache;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _policy = CachePolicy.From(settings);
            var memoryMB = settings.Number("MaxMemoryMB", 64, 1, 100_000);
            _cache = new ResponseCache(memoryMB * 1024L * 1024L, settings.Number("MaxEntries", 50_000, 10, 10_000_000), _clock);

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            Host.Log(NpPluginLogLevel.Information,
                $"Keeping GET responses for {_policy.DefaultTtlSeconds} second(s) unless they say otherwise, up to {memoryMB} MB.");

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            _cache.Clear();
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            _cache.Purge();

            //Only said when something happened since last time.
            if (_cache.Hits + _cache.Misses != _lastReportedHits)
            {
                _lastReportedHits = _cache.Hits + _cache.Misses;
                Host.Log(NpPluginLogLevel.Verbose,
                    $"Cache: {_cache.Hits} hit(s), {_cache.Misses} miss(es), {_cache.Count} kept ({_cache.Bytes / 1024} KB), {_cache.Evicted} dropped to make room.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (connection.Proxy.Traffic is not (NpTrafficKind.Http or NpTrafficKind.Https))
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning, "A client uses HTTP/2, which is compressed and is not cached. Untick Allow HTTP/2 on the proxy to have those requests cached.");
                    }

                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    var created = new CacheConnection();
                    created.Responses = new Http1StreamParser(isRequest: false,
                        head =>
                        {
                            //The answer to a HEAD request has the headers of a body that it does not have.
                            return head.StatusCode is >= 100 and < 200 ? false : created.Methods.TryPeek(out var method) && method == "HEAD";
                        },
                        () =>
                        {
                            created.Methods.TryDequeue(out _);
                            created.Outstanding = Math.Max(0, created.Outstanding - 1);
                        });
                    existing = created;
                    connection.State[StateKey] = existing;
                }

                var state = (CacheConnection)existing!;

                lock (state.Lock)
                {
                    if (data.Direction == NpDataDirection.ClientToEndpoint)
                    {
                        FromClient(state, data);
                    }
                    else
                    {
                        FromEndpoint(state, data);
                    }
                }
            }
            catch (Exception ex)
            {
                //Caching is never allowed to get in the way of the traffic.
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be cached: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        private void FromClient(CacheConnection state, NpDataEventArgs data)
        {
            if (state.Disabled)
            {
                return;
            }

            var requests = HeadGate.Feed(state.Reader, data);

            if (state.Reader.IsStopped)
            {
                state.Disabled = true; //Upgraded, or not HTTP: nothing here can be followed any more.
                state.Capture = null;
            }

            if (requests.Count == 0)
            {
                return;
            }

            state.Capture = null; //Whatever was being kept is not what the client is waiting for any more.

            if (requests.Count == 1 && state.Outstanding == 0 && state.Disabled == false)
            {
                var key = _policy.KeyFor(requests[0], data.Connection.ClientTls != null ? "https" : "http");

                if (key != null)
                {
                    if (data.CanRespond && _cache.Get(key) is { } hit)
                    {
                        var close = requests[0].BodyPending || requests[0].Header("Connection")?.Contains("close", StringComparison.OrdinalIgnoreCase) == true;
                        data.Respond(Serve(hit, close), close);
                        return; //Answered here: the endpoint never sees it, and nothing is waiting for an answer from it.
                    }

                    state.Capture = new Capture(key);
                }
            }

            foreach (var request in requests)
            {
                state.Outstanding++;
                state.Methods.Enqueue(request.Method);
            }
        }

        private void FromEndpoint(CacheConnection state, NpDataEventArgs data)
        {
            if (state.Disabled)
            {
                return;
            }

            state.Responses.Feed(data.Data.Span);

            if (state.Responses.IsStopped)
            {
                state.Disabled = true; //A response that ends when the connection does, or an upgrade.
            }

            if (state.Capture is not { } capture)
            {
                return;
            }

            if (capture.Feed(data.Data.Span, _policy.MaxObjectBytes))
            {
                state.Capture = null;
                Keep(capture);
            }
            else if (capture.Failed)
            {
                state.Capture = null;
            }
        }

        private void Keep(Capture capture)
        {
            var head = capture.Head!;

            if (_policy.TtlFor(head, capture.Body.Length) is not { } ttl)
            {
                return;
            }

            var kept = new HttpResponseHead { Version = "HTTP/1.1", Status = head.Status, Reason = head.Reason };
            foreach (var header in head.Headers.Where(o => NotKept.Contains(o.Key, StringComparer.OrdinalIgnoreCase) == false))
            {
                kept.Headers.Add(header);
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            _cache.Put(capture.Key, new CachedResponse { Head = kept, Body = capture.Body, StoredUtc = now, ExpiresUtc = now + ttl });
        }

        private byte[] Serve(CachedResponse response, bool close)
        {
            var head = new HttpResponseHead { Version = "HTTP/1.1", Status = response.Head.Status, Reason = response.Head.Reason };
            head.Headers.AddRange(response.Head.Headers);
            head.Set("Age", Math.Max(0, (int)(_clock.GetUtcNow().UtcDateTime - response.StoredUtc).TotalSeconds).ToString());
            head.Set("X-Cache", "HIT");
            head.Set("Connection", close ? "close" : "keep-alive");

            return [.. head.ToBytes(), .. response.Body];
        }
    }
}
