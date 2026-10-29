using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Text.RegularExpressions;

namespace NetProxy.Plugins.SecurityHeaders
{
    /// <summary>
    /// Adds the headers that harden a web site to every response, and takes out the ones that tell too much, without changing the site:
    ///
    ///   X-Content-Type-Options: nosniff          (ContentTypeOptions, on)
    ///   Referrer-Policy                          (ReferrerPolicy, strict-origin-when-cross-origin)
    ///   Strict-Transport-Security                (HstsSeconds, off until you set it; only on HTTPS)
    ///   X-Frame-Options, Content-Security-Policy, Permissions-Policy, Cross-Origin-Opener-Policy   (off until you set them)
    ///   Server, X-Powered-By, X-AspNet-Version... are taken out (RemoveHeaders)
    ///   Header1=Name: value, Header2=...         any header of your own
    ///
    /// A header that the site sends itself is left as it is, unless OnlyIfMissing is false. Strict-Transport-Security is sticky in
    /// browsers (they keep it for as long as it says), so it is not on by default: try a small HstsSeconds first.
    ///
    /// It works on HTTP/1.x responses (HTTP/2 is compressed, and is not changed), and the head of a response that was split over reads,
    /// with its start already sent on, is left as it is.
    /// </summary>
    [NpPlugin("Security Headers", Version = "1.0", Author = "NetworkDLS",
        Description = "Adds security headers (HSTS, nosniff, referrer policy, CSP, ...) to responses and removes the ones that give away the server.")]
    public sealed class SecurityHeadersPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.SecurityHeaders";
        private static readonly Regex HeaderName = new("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$", RegexOptions.Compiled);

        private sealed record Rule(string Name, string Value, bool OnlyOnHttps);

        private sealed class ConnectionState
        {
            public readonly object Lock = new();
            public readonly RequestReader Requests = new();
            public readonly Queue<string> Methods = new();
            public readonly List<(int Start, int End)> Heads = [];
            public Http1StreamParser Responses = null!;
        }

        private List<Rule> _rules = [];
        private List<string> _remove = [];
        private bool _onlyIfMissing = true;
        private int _warnedHttp2;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _onlyIfMissing = settings.Flag("OnlyIfMissing", true);
            _remove = settings.Has("RemoveHeaders") ? settings.List("RemoveHeaders") : ["Server", "X-Powered-By", "X-AspNet-Version", "X-AspNetMvc-Version"];
            _rules = [];

            if (settings.Flag("ContentTypeOptions", true))
            {
                Add(settings, "X-Content-Type-Options", "nosniff", false);
            }

            Add(settings, "Referrer-Policy", settings.Text("ReferrerPolicy", "strict-origin-when-cross-origin"), false);

            var hsts = settings.Number("HstsSeconds", 0, 0, 63_072_000);
            if (hsts > 0)
            {
                Add(settings, "Strict-Transport-Security",
                    $"max-age={hsts}{(settings.Flag("HstsIncludeSubDomains", false) ? "; includeSubDomains" : string.Empty)}{(settings.Flag("HstsPreload", false) ? "; preload" : string.Empty)}", true);
            }

            Add(settings, "X-Frame-Options", settings.Text("FrameOptions"), false);
            Add(settings, "Content-Security-Policy", settings.Text("ContentSecurityPolicy"), false);
            Add(settings, "Permissions-Policy", settings.Text("PermissionsPolicy"), false);
            Add(settings, "Cross-Origin-Opener-Policy", settings.Text("CrossOriginOpenerPolicy"), false);

            foreach (var (name, value) in settings.Numbered("Header"))
            {
                var colon = value.IndexOf(':');
                if (colon <= 0)
                {
                    settings.Problems.Add($"{name} is left out: a header is 'Name: value'.");
                }
                else
                {
                    Add(settings, value[..colon].Trim(), value[(colon + 1)..].Trim(), false);
                }
            }

            foreach (var name in _remove.Where(o => HeaderName.IsMatch(o) == false).ToList())
            {
                settings.Problems.Add($"'{name}' in the setting RemoveHeaders is not a header name, so it is left out.");
                _remove.Remove(name);
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            Host.Log(NpPluginLogLevel.Information, $"Adding {string.Join(", ", _rules.Select(o => o.Name))}; removing {(_remove.Count == 0 ? "nothing" : string.Join(", ", _remove))}.");
            return ValueTask.CompletedTask;
        }

        private void Add(SettingsReader settings, string name, string? value, bool onlyOnHttps)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (HeaderName.IsMatch(name) == false || value.Any(c => c < ' ' && c != '\t' || c == 0x7f))
            {
                settings.Problems.Add($"The header '{name}' is not left in: its name or its value has characters that a header can not have.");
                return;
            }

            _rules.RemoveAll(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            _rules.Add(new Rule(name, value, onlyOnHttps));
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                //The requests are only followed to know which responses are to a HEAD request, which have headers for a body they do not have.
                if (data.Direction == NpDataDirection.ClientToEndpoint && IsHttp(connection) && connection.IsHttp2 == false)
                {
                    var state = StateOf(connection);

                    lock (state.Lock)
                    {
                        foreach (var request in state.Requests.Feed(data.Data.Span))
                        {
                            state.Methods.Enqueue(request.Method);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be followed: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataSendingAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (data.Direction != NpDataDirection.EndpointToClient || IsHttp(connection) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning, "A client uses HTTP/2, which is compressed, so its responses are not changed. Untick Allow HTTP/2 on the proxy to have the headers added.");
                    }

                    return ValueTask.CompletedTask;
                }

                var state = StateOf(connection);

                lock (state.Lock)
                {
                    state.Heads.Clear();
                    state.Responses.Feed(data.Data.Span);

                    if (state.Heads.Count > 0)
                    {
                        data.Replace(Rewrite(data.Data.Span, state.Heads, connection.ClientTls != null));
                    }
                }
            }
            catch (Exception ex)
            {
                //The response goes on as it was.
                Host.Log(NpPluginLogLevel.Verbose, $"A response could not be changed: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        private static bool IsHttp(INpConnection connection) => connection.Proxy.Traffic is NpTrafficKind.Http or NpTrafficKind.Https;

        private ConnectionState StateOf(INpConnection connection)
        {
            if (connection.State.TryGetValue(StateKey, out var existing) && existing is ConnectionState found)
            {
                return found;
            }

            var created = new ConnectionState();
            created.Responses = new Http1StreamParser(isRequest: false,
                head =>
                {
                    if (head.StatusCode >= 200 && created.Responses.HeadStartInFeed >= 0)
                    {
                        created.Heads.Add((created.Responses.HeadStartInFeed, created.Responses.HeadEndInFeed));
                    }

                    //The answer to a HEAD request has the headers of a body that it does not have.
                    return head.StatusCode >= 200 && created.Methods.TryPeek(out var method) && method == "HEAD";
                },
                () => created.Methods.TryDequeue(out _));

            connection.State[StateKey] = created;
            return created;
        }

        /// <summary>
        /// The data with the heads that start in it changed. A head that can not be read is left as it was.
        /// </summary>
        private byte[] Rewrite(ReadOnlySpan<byte> data, List<(int Start, int End)> heads, bool isTls)
        {
            var result = new List<byte>(data.Length + 256);
            var position = 0;

            foreach (var (start, end) in heads)
            {
                result.AddRange(data[position..start].ToArray());

                var head = HttpResponseHead.TryParse(data[start..(end - 4)]);
                if (head == null)
                {
                    result.AddRange(data[start..end].ToArray());
                }
                else
                {
                    Apply(head, isTls);
                    result.AddRange(head.ToBytes());
                }

                position = end;
            }

            result.AddRange(data[position..].ToArray());
            return [.. result];
        }

        internal void Apply(HttpResponseHead head, bool isTls)
        {
            foreach (var name in _remove)
            {
                head.Remove(name);
            }

            foreach (var rule in _rules)
            {
                if ((rule.OnlyOnHttps && isTls == false) || (_onlyIfMissing && head.Has(rule.Name)))
                {
                    continue;
                }

                head.Set(rule.Name, rule.Value);
            }
        }
    }
}
