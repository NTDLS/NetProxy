using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Plugins.TrafficSplit
{
    /// <summary>
    /// Sends a share of the clients to some endpoints first: a canary release (10% of clients to the new version), a blue/green switch
    /// (0% to 100% in steps), or keeping the clients of one network on one server. The rules are the settings Rule1, Rule2, ...:
    ///
    ///   Rule1=proxy=web; endpoints=10.0.0.6:8080; percent=10
    ///   Rule2=endpoints=10.0.0.7:8080, 10.0.0.8:8080; percent=50; sticky=connection; mode=only
    ///
    /// * proxy: the name of the proxy the rule is for (default: every proxy that uses the plug-in).
    /// * endpoints: the endpoints the share goes to, as address:port as they are in the proxy.
    /// * percent: how many clients out of 100 (0 to 100, fractions allowed: 0.5).
    /// * sticky: client (default) sends the same client address to the same side every time, so a user keeps seeing the same version;
    ///   connection chooses for every connection.
    /// * mode: prefer (default) puts the endpoints first for the clients that are chosen, and the other endpoints first for the rest,
    ///   with the others behind as fall-backs so that nobody is turned away while any endpoint works; only leaves just those endpoints.
    ///
    /// The choice is made when a client connects, before its request is read, so it can not be made by a cookie or a header. Clients that
    /// connect through a proxy of their own (Cloudflare) all have the address of that proxy: use sticky=connection for those.
    /// </summary>
    [NpPlugin("Traffic Split", Version = "1.0", Author = "NetworkDLS",
        Description = "Sends a percentage of clients to chosen endpoints first (canary releases, blue/green), with the other endpoints as fall-backs.")]
    public sealed class TrafficSplitPlugin : NpPlugin
    {
        internal sealed class SplitRule
        {
            public string Text { get; init; } = string.Empty;
            public string? Proxy { get; init; }
            public List<(string Address, int Port)> Endpoints { get; init; } = [];
            public double Percent { get; init; }
            public bool StickyToClient { get; init; } = true;
            public bool Only { get; init; }
            public string Salt { get; init; } = string.Empty;

            public bool Matches(NpEndpointInfo endpoint)
                => Endpoints.Any(o => o.Port == endpoint.Port && string.Equals(o.Address, endpoint.Address.Trim('[', ']'), StringComparison.OrdinalIgnoreCase));
        }

        private readonly ConcurrentDictionary<string, long> _counts = new();
        private List<SplitRule> _rules = [];

        public override TimeSpan? TickInterval => TimeSpan.FromMinutes(1);

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);
            _rules = [];

            foreach (var (name, text) in settings.Numbered("Rule"))
            {
                var rule = Parse(name, text, out var error);
                if (rule == null)
                {
                    settings.Problems.Add($"{name} is left out. {error}");
                }
                else
                {
                    _rules.Add(rule);
                }
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            foreach (var rule in _rules)
            {
                Host.Log(NpPluginLogLevel.Information,
                    $"{rule.Percent:0.##}% of clients{(rule.Proxy != null ? $" of '{rule.Proxy}'" : string.Empty)} go {(rule.Only ? "only " : "first ")}to {string.Join(", ", rule.Endpoints.Select(o => $"{o.Address}:{o.Port}"))}.");
            }

            if (_rules.Count == 0)
            {
                Host.Log(NpPluginLogLevel.Warning, "There are no rules (Rule1=endpoints=10.0.0.6:8080; percent=10), so nothing is split.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            if (_counts.IsEmpty == false)
            {
                var report = _counts.OrderBy(o => o.Key).Select(o => $"{o.Key}: {o.Value}").ToList();
                _counts.Clear();
                Host.Log(NpPluginLogLevel.Information, $"In the last minute: {string.Join("; ", report)}.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnEndpointsOrderingAsync(NpEndpointOrderEventArgs order, CancellationToken cancellationToken)
        {
            try
            {
                var connection = order.Connection;

                for (var i = 0; i < _rules.Count; i++)
                {
                    var rule = _rules[i];

                    if (rule.Proxy != null && rule.Proxy.Equals(connection.Proxy.Name, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }

                    var listed = order.Endpoints.Where(rule.Matches).Select(o => o.Id).ToList();
                    if (listed.Count == 0 || listed.Count == order.Endpoints.Count)
                    {
                        continue; //Nothing to choose between.
                    }

                    var chosen = IsChosen(rule, connection);
                    _counts.AddOrUpdate($"rule {i + 1} {(chosen ? "listed" : "others")}", 1, (_, n) => n + 1);

                    if (chosen)
                    {
                        if (rule.Only)
                        {
                            order.Only(listed);
                        }
                        else
                        {
                            order.Prefer(listed);
                        }
                    }
                    else
                    {
                        //The rest of the clients go to the other endpoints first. The listed ones stay behind them, unless this is "only".
                        var others = order.Endpoints.Where(o => listed.Contains(o.Id) == false).Select(o => o.Id).ToList();
                        if (rule.Only)
                        {
                            order.Only(others);
                        }
                        else
                        {
                            order.Prefer(others);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //The endpoints stay in the order they were in.
                Host.Log(NpPluginLogLevel.Warning, $"The endpoints could not be split: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Whether this client is one of the share. For a client the same answer every time: the same address always lands on the same side.
        /// </summary>
        internal static bool IsChosen(SplitRule rule, INpConnection connection)
        {
            if (rule.Percent <= 0)
            {
                return false;
            }

            if (rule.Percent >= 100)
            {
                return true;
            }

            var key = rule.StickyToClient
                ? rule.Salt + "|" + (AddressRange.TryParseAddress(connection.ClientAddress, out var address) ? AddressRange.KeyOf(address) : connection.ClientAddress)
                : rule.Salt + "|" + Guid.NewGuid();

            //0 to 9999, evenly, from the hash of the key.
            var bucket = BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0) % 10_000;
            return bucket < rule.Percent * 100;
        }

        internal static SplitRule? Parse(string name, string text, out string? error)
        {
            error = null;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = part.IndexOf('=');
                if (equals <= 0)
                {
                    error = $"'{part}' is not name=value.";
                    return null;
                }

                values[part[..equals].Trim()] = part[(equals + 1)..].Trim();
            }

            foreach (var key in values.Keys.Except(["proxy", "endpoints", "percent", "sticky", "mode"], StringComparer.OrdinalIgnoreCase))
            {
                error = $"'{key}' is not part of a rule: use proxy, endpoints, percent, sticky and mode.";
                return null;
            }

            if (values.TryGetValue("endpoints", out var endpointsText) == false || string.IsNullOrWhiteSpace(endpointsText))
            {
                error = "A rule needs endpoints=address:port.";
                return null;
            }

            var endpoints = new List<(string, int)>();
            foreach (var endpoint in endpointsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = endpoint.LastIndexOf(':');
                if (colon <= 0 || int.TryParse(endpoint[(colon + 1)..], out var port) == false || port is < 1 or > 65535)
                {
                    error = $"'{endpoint}' is not an endpoint: use address:port.";
                    return null;
                }

                endpoints.Add((endpoint[..colon].Trim('[', ']'), port));
            }

            if (values.GetValueOrDefault("percent") is not { } percentText
                || double.TryParse(percentText.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent) == false
                || percent is < 0 or > 100)
            {
                error = "A rule needs percent=0 to 100.";
                return null;
            }

            var sticky = values.GetValueOrDefault("sticky", "client");
            if (sticky.Equals("client", StringComparison.OrdinalIgnoreCase) == false && sticky.Equals("connection", StringComparison.OrdinalIgnoreCase) == false)
            {
                error = "sticky is client or connection.";
                return null;
            }

            var mode = values.GetValueOrDefault("mode", "prefer");
            if (mode.Equals("prefer", StringComparison.OrdinalIgnoreCase) == false && mode.Equals("only", StringComparison.OrdinalIgnoreCase) == false)
            {
                error = "mode is prefer or only.";
                return null;
            }

            return new SplitRule
            {
                Text = text,
                Proxy = values.GetValueOrDefault("proxy"),
                Endpoints = endpoints,
                Percent = percent,
                StickyToClient = sticky.Equals("client", StringComparison.OrdinalIgnoreCase),
                Only = mode.Equals("only", StringComparison.OrdinalIgnoreCase),
                Salt = name.ToLowerInvariant() //Each rule splits the clients its own way.
            };
        }
    }
}
