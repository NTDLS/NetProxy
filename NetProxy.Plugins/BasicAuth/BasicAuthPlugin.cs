using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Net;
using System.Text;

namespace NetProxy.Plugins.BasicAuth
{
    /// <summary>
    /// Asks for a user name and a password (HTTP Basic authentication) before a request reaches the endpoint: to protect an admin page, a
    /// staging site or a tool that has no login of its own, without changing it. Paths (default: everything) need a login except
    /// ExcludePaths, and clients in Allow (an office network) do not need one. Users are the settings User1, User2, ...:
    ///
    ///   User1=alice:a password
    ///   User2=bob:pbkdf2:200000:SALT:HASH
    ///
    /// Passwords can be kept as sha256:HEX or pbkdf2:ITERATIONS:SALT:HASH (see the README) so that they are not in the settings. Too many
    /// wrong passwords from an address locks it out for a while, also for the right one.
    ///
    /// Basic authentication sends the password with every request, so use it on an HTTPS proxy. HTTP/2 can not be checked (it is
    /// compressed), so a connection that speaks it is closed: untick Allow HTTP/2 on a proxy that uses this plug-in. The same goes for
    /// anything that is not HTTP/1.x: it is closed, because it can not be checked. A connection that was upgraded (a WebSocket) after a
    /// request that was allowed carries on.
    /// </summary>
    [NpPlugin("Basic Auth", Version = "1.0", Author = "NetworkDLS",
        Description = "Asks for a user name and password (HTTP Basic) for the paths you choose, before a request reaches the endpoint.")]
    public sealed class BasicAuthPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.BasicAuth";

        private readonly TimeProvider _clock;
        private readonly Dictionary<string, string> _users = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<DateTime>> _failures = [];
        private readonly Dictionary<string, DateTime> _lockedUntil = [];
        private TrustedProxies _trusted = new([], [], "TrustedProxies");
        private List<AddressRange> _allow = [];
        private List<string> _paths = ["/"], _excludePaths = [], _hosts = [];
        private string _realm = "Restricted";
        private int _maxFailures = 10, _failureWindowMinutes = 10, _lockoutMinutes = 10;
        private int _warnedHttp2;

        public BasicAuthPlugin() : this(TimeProvider.System)
        {
        }

        internal BasicAuthPlugin(TimeProvider clock) => _clock = clock;

        public override TimeSpan? TickInterval => TimeSpan.FromMinutes(1);

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _realm = settings.Text("Realm", "Restricted").Replace("\"", "'");
            _maxFailures = settings.Number("MaxFailures", 10, 1, 10_000);
            _failureWindowMinutes = settings.Number("FailureWindowMinutes", 10, 1, 1440);
            _lockoutMinutes = settings.Number("LockoutMinutes", 10, 1, 10_080);
            _paths = settings.Has("Paths") ? settings.List("Paths") : ["/"];
            _excludePaths = settings.List("ExcludePaths");
            _hosts = settings.List("Hosts").Select(o => o.ToLowerInvariant()).ToList();
            _trusted = new TrustedProxies(settings.List("TrustedProxies"), settings.Problems, "TrustedProxies");

            _allow = [];
            foreach (var text in settings.List("Allow"))
            {
                if (AddressRange.TryParse(text, out var range))
                {
                    _allow.Add(range!);
                }
                else
                {
                    settings.Problems.Add($"'{text}' in the setting Allow is not an address or a range of addresses, so it is left out.");
                }
            }

            _users.Clear();
            foreach (var (name, value) in settings.Numbered("User"))
            {
                var colon = value.IndexOf(':');
                if (colon <= 0 || colon == value.Length - 1)
                {
                    settings.Problems.Add($"{name} is left out: a user is 'name:password'.");
                }
                else if (PasswordCheck.IsWellFormed(value[(colon + 1)..]) == false)
                {
                    settings.Problems.Add($"{name} is left out: the hash of the password of '{value[..colon]}' is not well formed.");
                }
                else if (_users.TryAdd(value[..colon], value[(colon + 1)..]) == false)
                {
                    settings.Problems.Add($"{name} is left out: there is already a user '{value[..colon]}'.");
                }
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            if (_users.Count == 0)
            {
                Host.Log(NpPluginLogLevel.Warning, "There are no users (User1=name:password), so nobody can log in to the paths that are protected.");
            }

            Host.Log(NpPluginLogLevel.Information, $"{_users.Count} user(s); a login is asked for on {string.Join(", ", _paths)}"
                + (_excludePaths.Count > 0 ? $" except {string.Join(", ", _excludePaths)}" : string.Empty) + ".");

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            lock (_failures)
            {
                foreach (var key in _failures.Keys.ToList())
                {
                    _failures[key].RemoveAll(o => now - o > TimeSpan.FromMinutes(_failureWindowMinutes));
                    if (_failures[key].Count == 0)
                    {
                        _failures.Remove(key);
                    }
                }

                foreach (var key in _lockedUntil.Where(o => o.Value <= now).Select(o => o.Key).ToList())
                {
                    _lockedUntil.Remove(key);
                }
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            var connection = data.Connection;

            if (data.Direction != NpDataDirection.ClientToEndpoint || connection.Proxy.Traffic is not (NpTrafficKind.Http or NpTrafficKind.Https))
            {
                return ValueTask.CompletedTask;
            }

            try
            {
                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning,
                            "A client uses HTTP/2, which can not be checked, so its connection is closed. Untick Allow HTTP/2 on the proxy to let those clients in with a login.");
                    }

                    data.CloseConnection("HTTP/2 can not be checked for a login.");
                    return ValueTask.CompletedTask;
                }

                if (AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false)
                {
                    data.CloseConnection("The client address is not known.");
                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    existing = new RequestReader(keepBuffer: true);
                    connection.State[StateKey] = existing;
                }

                var reader = (RequestReader)existing!;
                var requests = HeadGate.Feed(reader, data);

                if (reader.IsFaulted)
                {
                    //Not HTTP/1.x, or a head that is too big: it can not be checked, and what can not be checked does not get through.
                    data.CloseConnection("The request can not be checked for a login.");
                    return ValueTask.CompletedTask;
                }

                foreach (var request in requests)
                {
                    var client = _trusted.Client(peer, request);
                    if (Decide(client, request) is not { } refusal)
                    {
                        continue;
                    }

                    var close = refusal.MustClose || requests.Count > 1 || request.BodyPending;
                    if (data.CanRespond)
                    {
                        data.Respond(refusal.Build(close), close);
                    }
                    else
                    {
                        data.CloseConnection("A login is needed.");
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                //What can not be checked does not get through.
                Host.Log(NpPluginLogLevel.Warning, $"A request could not be checked for a login, so its connection is closed: {ex.GetBaseException().Message}");
                data.CloseConnection("The request can not be checked for a login.");
            }

            return ValueTask.CompletedTask;
        }

        private sealed record Refusal(Func<bool, byte[]> Build, bool MustClose);

        /// <summary>
        /// Null when the request can go to the endpoint, otherwise the response that it is answered with.
        /// </summary>
        private Refusal? Decide(IPAddress client, SeenRequest request)
        {
            if (IsProtected(request) == false || _allow.Any(o => o.Contains(client)))
            {
                return null;
            }

            var key = AddressRange.KeyOf(client);
            var now = _clock.GetUtcNow().UtcDateTime;

            lock (_failures)
            {
                if (_lockedUntil.TryGetValue(key, out var until) && until > now)
                {
                    var retryAfter = Math.Max(1, (int)Math.Ceiling((until - now).TotalSeconds)).ToString();
                    return new Refusal(close => HttpResponse.Text(429, "Too many wrong passwords. Try again later.", close, [("Retry-After", retryAfter)]), MustClose: true);
                }
            }

            var user = Credentials(request, out var password);

            if (user != null && Check(user, password!))
            {
                return null;
            }

            if (request.Header("Authorization") != null)
            {
                Fail(key, user, client, now);
            }

            return new Refusal(close => HttpResponse.Text(401, "A user name and a password are needed.", close,
                [("WWW-Authenticate", $"Basic realm=\"{_realm}\", charset=\"UTF-8\""), ("Cache-Control", "no-store")]), MustClose: false);
        }

        private bool IsProtected(SeenRequest request)
        {
            var path = request.Path;
            var host = ExchangeTracker.NormalizeHost(request.Host) ?? string.Empty;

            if (_hosts.Count > 0 && _hosts.Any(o => o.StartsWith("*.") ? host.EndsWith(o[1..], StringComparison.Ordinal) : host == o) == false)
            {
                return false;
            }

            return _paths.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase)) && _excludePaths.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase)) == false;
        }

        /// <summary>
        /// The user name and password of an "Authorization: Basic ..." header, or null.
        /// </summary>
        private static string? Credentials(SeenRequest request, out string? password)
        {
            password = null;
            var header = request.Header("Authorization");

            if (header == null || header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) == false)
            {
                return null;
            }

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                var colon = decoded.IndexOf(':');
                if (colon <= 0)
                {
                    return null;
                }

                password = decoded[(colon + 1)..];
                return decoded[..colon];
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private bool Check(string user, string password)
        {
            if (_users.TryGetValue(user, out var stored))
            {
                return PasswordCheck.Verify(stored, password);
            }

            //An unknown user takes as long as a known one, so the time does not say which names exist.
            PasswordCheck.Verify("x", password);
            return false;
        }

        private void Fail(string key, string? user, IPAddress client, DateTime now)
        {
            lock (_failures)
            {
                if (_failures.TryGetValue(key, out var list) == false)
                {
                    _failures[key] = list = [];
                }

                list.RemoveAll(o => now - o > TimeSpan.FromMinutes(_failureWindowMinutes));
                list.Add(now);

                if (list.Count >= _maxFailures)
                {
                    _lockedUntil[key] = now.AddMinutes(_lockoutMinutes);
                    list.Clear();
                    Host.Log(NpPluginLogLevel.Warning, $"{client} gave {_maxFailures} wrong passwords and is locked out for {_lockoutMinutes} minute(s).");
                }
            }

            Host.Log(NpPluginLogLevel.Verbose, $"{client} gave a wrong password for '{Printable(user)}'.");
        }

        private static string Printable(string? text) => new((text ?? string.Empty).Where(c => c >= ' ' && c != 0x7f).Take(64).ToArray());
    }
}
