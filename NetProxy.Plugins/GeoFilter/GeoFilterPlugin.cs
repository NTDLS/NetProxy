using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Collections.Concurrent;
using System.Net;

namespace NetProxy.Plugins.GeoFilter
{
    /// <summary>
    /// Lets clients in or keeps them out by where they are: the country (AllowCountries, DenyCountries) and the network, the autonomous
    /// system (DenyAsns, AllowAsns: a hosting company that scanners come from, say). It reads a country database and an ASN database in the
    /// MaxMind format (.mmdb): GeoLite2-Country and GeoLite2-ASN (free, with an account at maxmind.com) or the free ones of DB-IP.
    /// The files are looked at once a minute, so a database that is updated is picked up without a restart.
    ///
    /// A client that connects directly is turned away when it connects. Behind Cloudflare or another proxy set TrustedProxies: the client
    /// is then found in the request (CF-Connecting-IP, X-Forwarded-For) and answered with a 403, and if there is no database the country
    /// that Cloudflare puts in CF-IPCountry is used.
    ///
    /// With LogOnly nobody is turned away, and the log says who would have been.
    /// </summary>
    [NpPlugin("Geo Filter", Version = "1.0", Author = "NetworkDLS",
        Description = "Allows or denies clients by country and network (MaxMind .mmdb databases, or Cloudflare's CF-IPCountry header).")]
    public sealed class GeoFilterPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.GeoFilter";

        private readonly object _lookupLock = new();
        private readonly ConcurrentDictionary<string, long> _turnedAway = new();
        private GeoPolicy _policy = new();
        private TrustedProxies _trusted = new([], [], "TrustedProxies");
        private IGeoLookup? _lookup;
        private Func<IGeoLookup?>? _load;
        private (string? Country, string? Asn) _loadedStamp;
        private string? _countryFile, _asnFile;
        private bool _logOnly;
        private long _count;

        public override TimeSpan? TickInterval => TimeSpan.FromSeconds(60);

        public GeoFilterPlugin()
        {
        }

        /// <summary>For the tests: a lookup that is not a database.</summary>
        internal GeoFilterPlugin(IGeoLookup lookup) => _lookup = lookup;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _logOnly = settings.Flag("LogOnly", false);
            _trusted = new TrustedProxies(settings.List("TrustedProxies"), settings.Problems, "TrustedProxies");

            var allow = new List<AddressRange>();
            foreach (var text in settings.List("Allow"))
            {
                if (AddressRange.TryParse(text, out var range))
                {
                    allow.Add(range!);
                }
                else
                {
                    settings.Problems.Add($"'{text}' in the setting Allow is not an address or a range of addresses, so it is left out.");
                }
            }

            _policy = new GeoPolicy
            {
                AllowCountries = Countries(settings, "AllowCountries"),
                DenyCountries = Countries(settings, "DenyCountries"),
                DenyAsns = Asns(settings, "DenyAsns"),
                AllowAsns = Asns(settings, "AllowAsns"),
                AllowUnknown = settings.Flag("AllowUnknown", true),
                AllowPrivate = settings.Flag("AllowPrivate", true),
                Allow = allow
            };

            _countryFile = settings.Text("CountryDatabase") is { } c ? Path.GetFullPath(c, Host.DataDirectory) : null;
            _asnFile = settings.Text("AsnDatabase") is { } a ? Path.GetFullPath(a, Host.DataDirectory) : null;

            if (_lookup == null && (_countryFile != null || _asnFile != null))
            {
                foreach (var file in new[] { _countryFile, _asnFile }.OfType<string>().Where(o => File.Exists(o) == false))
                {
                    settings.Problems.Add($"The database '{file}' does not exist yet. It is loaded when it does.");
                }

                _load = () => new MaxMindLookup(ExistingOrNull(_countryFile), ExistingOrNull(_asnFile));
                Reload(first: true);
            }

            if (_policy.DenyAsns.Count + _policy.AllowAsns.Count > 0 && _asnFile == null && _lookup == null)
            {
                settings.Problems.Add("DenyAsns and AllowAsns need an AsnDatabase, so they do nothing.");
            }

            if (_policy.HasRules && _lookup == null && _trusted.Any == false)
            {
                settings.Problems.Add("There are rules, but no CountryDatabase and no TrustedProxies to take CF-IPCountry from, so nobody can be placed: set CountryDatabase to a .mmdb file.");
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            Host.Log(NpPluginLogLevel.Information, _policy.HasRules
                ? $"{(_logOnly ? "Log-only: " : string.Empty)}allow {Describe(_policy.AllowCountries)}, deny {Describe(_policy.DenyCountries)}{(_policy.DenyAsns.Count > 0 ? $", deny networks {string.Join(", ", _policy.DenyAsns.Select(o => "AS" + o))}" : string.Empty)}."
                : "There are no rules (AllowCountries, DenyCountries, DenyAsns), so everyone is let in.");

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            lock (_lookupLock)
            {
                (_lookup as IDisposable)?.Dispose();
                _lookup = null;
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            Reload(first: false);

            var count = Interlocked.Exchange(ref _count, 0);
            if (count > 0)
            {
                var top = _turnedAway.OrderByDescending(o => o.Value).Take(5).Select(o => $"{o.Key} ({o.Value})").ToList();
                _turnedAway.Clear();
                Host.Log(NpPluginLogLevel.Information, $"{(_logOnly ? "Would have turned away" : "Turned away")} {count} connection(s) and request(s) in the last minute: {string.Join(", ", top)}.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                if (connection.Proxy.Traffic == NpTrafficKind.Udp || _policy.HasRules == false
                    || AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false
                    //Everyone connects from a proxy that is trusted. The clients are told apart by their requests.
                    || _trusted.IsTrusted(peer))
                {
                    return ValueTask.FromResult(NpVerdict.Allow);
                }

                if (Check(peer, null) is { } reason)
                {
                    return ValueTask.FromResult(Turn(peer, reason, connection));
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A connection could not be checked: {ex.GetBaseException().Message}");
            }

            return ValueTask.FromResult(NpVerdict.Allow);
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (data.Direction != NpDataDirection.ClientToEndpoint || _policy.HasRules == false || _trusted.Any == false
                    || connection.Proxy.Traffic is not (NpTrafficKind.Http or NpTrafficKind.Https)
                    || connection.IsHttp2
                    || AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false
                    || _trusted.IsTrusted(peer) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    existing = new RequestReader(keepBuffer: true);
                    connection.State[StateKey] = existing;
                }

                foreach (var request in HeadGate.Feed((RequestReader)existing!, data))
                {
                    var client = _trusted.Client(peer, request);
                    var header = request.Header("CF-IPCountry");
                    var hint = header is { Length: 2 } && header.All(char.IsAsciiLetter) && header.Equals("XX", StringComparison.OrdinalIgnoreCase) == false
                        ? header.ToUpperInvariant() : null;

                    if (Check(client, hint) is { } reason && Turn(client, reason, connection).Allowed == false)
                    {
                        if (data.CanRespond)
                        {
                            var close = request.BodyPending;
                            data.Respond(HttpResponse.Text(403, "Access from your location is not available.", close), close);
                        }
                        else
                        {
                            data.CloseConnection($"{client} is not let in ({reason}).");
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be checked: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Null when the client is let in, otherwise why not.
        /// </summary>
        internal string? Check(IPAddress client, string? countryHint)
        {
            IGeoLookup? lookup;
            lock (_lookupLock)
            {
                lookup = _lookup;
            }

            return _policy.Check(client, lookup, countryHint);
        }

        private NpVerdict Turn(IPAddress client, string reason, INpConnection connection)
        {
            Interlocked.Increment(ref _count);
            _turnedAway.AddOrUpdate($"{reason}", 1, (_, n) => n + 1);

            if (_turnedAway.Count < 1000 && _turnedAway.GetValueOrDefault(reason) == 1)
            {
                Host.Log(NpPluginLogLevel.Information, $"{(_logOnly ? "Would turn away" : "Turning away")} {client} ({reason}) on proxy '{connection.Proxy.Name}'.");
            }

            return _logOnly ? NpVerdict.Allow : NpVerdict.Deny($"{client} is not let in ({reason}).");
        }

        private static HashSet<string> Countries(SettingsReader settings, string name)
        {
            var countries = new HashSet<string>();

            foreach (var code in settings.List(name))
            {
                if (code.Length == 2 && code.All(char.IsAsciiLetter))
                {
                    countries.Add(code.ToUpperInvariant());
                }
                else
                {
                    settings.Problems.Add($"'{code}' in the setting {name} is not a two letter country code (US, DE, ...), so it is left out.");
                }
            }

            return countries;
        }

        private static HashSet<long> Asns(SettingsReader settings, string name)
        {
            var numbers = new HashSet<long>();

            foreach (var text in settings.List(name))
            {
                if (long.TryParse(text.StartsWith("AS", StringComparison.OrdinalIgnoreCase) ? text[2..] : text, out var number) && number > 0)
                {
                    numbers.Add(number);
                }
                else
                {
                    settings.Problems.Add($"'{text}' in the setting {name} is not an AS number (13335 or AS13335), so it is left out.");
                }
            }

            return numbers;
        }

        private static string? ExistingOrNull(string? file) => file != null && File.Exists(file) ? file : null;

        /// <summary>
        /// Loads the databases, again when a file has changed.
        /// </summary>
        private void Reload(bool first)
        {
            if (_load == null)
            {
                return;
            }

            var stamp = (Stamp(_countryFile), Stamp(_asnFile));
            if (first == false && stamp == _loadedStamp)
            {
                return;
            }

            try
            {
                var fresh = ExistingOrNull(_countryFile) != null || ExistingOrNull(_asnFile) != null ? _load() : null;

                IGeoLookup? old;
                lock (_lookupLock)
                {
                    old = _lookup;
                    _lookup = fresh;
                }

                _loadedStamp = stamp;
                (old as IDisposable)?.Dispose();

                if (first == false && fresh != null)
                {
                    Host.Log(NpPluginLogLevel.Information, "The geo databases changed and are loaded again.");
                }
            }
            catch (Exception ex)
            {
                //The old databases stay in use.
                _loadedStamp = stamp;
                Host.Log(NpPluginLogLevel.Warning, $"The geo databases could not be read{(_lookup != null ? ", so the ones from before are kept" : string.Empty)}: {ex.GetBaseException().Message}");
            }
        }

        private static string? Stamp(string? file) => file != null && File.Exists(file) ? $"{File.GetLastWriteTimeUtc(file):O}|{new FileInfo(file).Length}" : null;

        private static string Describe(HashSet<string> countries) => countries.Count == 0 ? "-" : string.Join(" ", countries.Order());
    }
}
