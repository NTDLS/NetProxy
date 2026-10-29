using System.Net;

using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.AntiHammer
{
    /// <summary>
    /// What the plug-in is set up with: the name=value lines of its settings in the NetProxy console. Every setting has a default, so a
    /// plug-in with no settings at all watches and logs what it would ban, and bans nobody.
    /// </summary>
    public sealed class AntiHammerSettings
    {
        /// <summary>
        /// Only log the bans that would be made. This is the default, so that the limits can be checked against the real traffic before
        /// anyone is turned away. Setting: LogOnly (set it to false to ban)
        /// </summary>
        public bool LogOnly { get; init; } = true;

        /// <summary>How long the first ban lasts. Setting: BanMinutes</summary>
        public int BanMinutes { get; init; } = 60;

        /// <summary>An address that is banned again after a ban is banned for twice as long as the last time. Setting: Escalate</summary>
        public bool Escalate { get; init; } = true;

        /// <summary>The longest that a ban can get to. Setting: MaxBanHours</summary>
        public int MaxBanHours { get; init; } = 168;

        /// <summary>How long it is remembered that an address was banned, for the escalation. Setting: OffenceMemoryHours</summary>
        public int OffenceMemoryHours { get; init; } = 168;

        /// <summary>More requests than this from an address in the window is a ban. Setting: MaxRequests, RequestWindowSeconds</summary>
        public int MaxRequests { get; init; } = 200;
        public int RequestWindowSeconds { get; init; } = 10;

        /// <summary>More requests than this for the same path from an address in the window is a ban. Setting: MaxSamePath, SamePathWindowSeconds</summary>
        public int MaxSamePath { get; init; } = 120;
        public int SamePathWindowSeconds { get; init; } = 60;

        /// <summary>More connections than this from an address in the window is a ban. Setting: MaxConnections, ConnectionWindowSeconds</summary>
        public int MaxConnections { get; init; } = 100;
        public int ConnectionWindowSeconds { get; init; } = 10;

        /// <summary>This many requests for pages that nobody has in the window is a ban. Setting: MaxProbes, ProbeWindowMinutes</summary>
        public int MaxProbes { get; init; } = 3;
        public int ProbeWindowMinutes { get; init; } = 10;

        /// <summary>
        /// The paths that only something that is looking for a weakness asks for. A path that starts with one of these is a probe; one that
        /// starts with a * is a probe when it holds the text anywhere. Setting: ProbePaths (replaces these), ExtraProbePaths (adds to them)
        /// </summary>
        public IReadOnlyList<string> ProbePaths { get; init; } = DefaultProbePaths;

        /// <summary>Never banned. Addresses and ranges ("203.0.113.0/24"), comma separated. Setting: Allow</summary>
        public IReadOnlyList<string> Allow { get; init; } = [];

        /// <summary>Loopback and private addresses are never banned. Setting: AllowPrivate</summary>
        public bool AllowPrivate { get; init; } = true;

        /// <summary>
        /// The proxies that this NetProxy is behind. A request from one of them says who the client is in CF-Connecting-IP or
        /// X-Forwarded-For, and is believed. Anyone else who sends those headers is not. "cloudflare" stands for the addresses of
        /// Cloudflare. Comma separated. Setting: TrustedProxies
        /// </summary>
        public IReadOnlyList<string> TrustedProxies { get; init; } = [];

        public static readonly IReadOnlyList<string> DefaultProbePaths =
        [
            "/.env", "/.git", "/.svn", "/.hg", "/.aws", "/.ssh", "/.ds_store", "/.vscode", "/.idea", "/.htaccess", "/.htpasswd",
            "/wp-login.php", "/wp-admin", "/wp-content", "/wp-includes", "/wp-config", "/wordpress", "/xmlrpc.php",
            "/phpmyadmin", "/pma/", "/myadmin", "/mysql", "/phpinfo", "/info.php", "/adminer", "/vendor/phpunit", "/cgi-bin/",
            "/boaform", "/hnap1", "/actuator", "/server-status", "/solr/", "/console/", "/manager/html", "/owa/auth",
            "/config.json", "/config.php", "/backup.sql", "/db.sql", "/dump.sql", "/id_rsa",
            "*eval-stdin.php", "*/etc/passwd", "*../", "*..%2f", "*%2e%2e", "*<script", "*union+select", "*union%20select"
        ];

        private IReadOnlyList<AddressRange> _allowRanges = [];
        private IReadOnlyList<AddressRange> _trustedRanges = [];

        public TimeSpan BanDuration => TimeSpan.FromMinutes(BanMinutes);
        public TimeSpan MaxBanDuration => TimeSpan.FromHours(Math.Max(MaxBanHours, 1));
        public TimeSpan OffenceMemory => TimeSpan.FromHours(OffenceMemoryHours);

        /// <summary>The longest of the windows: counters that have not been touched for this long are of no use any more.</summary>
        public TimeSpan LongestWindow => TimeSpan.FromSeconds(Math.Max(Math.Max(RequestWindowSeconds, SamePathWindowSeconds),
            Math.Max(ConnectionWindowSeconds, ProbeWindowMinutes * 60)));

        public static AntiHammerSettings From(IReadOnlyDictionary<string, string> settings, out List<string> problems)
        {
            problems = [];
            var found = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            var errors = problems;

            int Number(string name, int fallback, int min, int max)
            {
                if (found.TryGetValue(name, out var value) && string.IsNullOrWhiteSpace(value) == false)
                {
                    if (int.TryParse(value, out var number) && number >= min && number <= max)
                    {
                        return number;
                    }

                    errors.Add($"The setting {name} must be a number from {min} to {max}; '{value}' is not, so {fallback} is used.");
                }

                return fallback;
            }

            bool Flag(string name, bool fallback)
            {
                if (found.TryGetValue(name, out var value) && string.IsNullOrWhiteSpace(value) == false)
                {
                    if (bool.TryParse(value, out var flag))
                    {
                        return flag;
                    }

                    errors.Add($"The setting {name} must be true or false; '{value}' is not, so {fallback} is used.");
                }

                return fallback;
            }

            List<string> List(string name)
                => found.TryGetValue(name, out var value)
                    ? value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                    : [];

            var probes = found.ContainsKey("ProbePaths") ? List("ProbePaths") : DefaultProbePaths.ToList();
            probes.AddRange(List("ExtraProbePaths"));

            var allow = List("Allow");
            var trusted = List("TrustedProxies");

            var result = new AntiHammerSettings
            {
                LogOnly = Flag("LogOnly", true),
                BanMinutes = Number("BanMinutes", 60, 1, 525_600),
                Escalate = Flag("Escalate", true),
                MaxBanHours = Number("MaxBanHours", 168, 1, 87_600),
                OffenceMemoryHours = Number("OffenceMemoryHours", 168, 1, 87_600),
                MaxRequests = Number("MaxRequests", 200, 2, 1_000_000),
                RequestWindowSeconds = Number("RequestWindowSeconds", 10, 1, 3600),
                MaxSamePath = Number("MaxSamePath", 120, 2, 1_000_000),
                SamePathWindowSeconds = Number("SamePathWindowSeconds", 60, 1, 3600),
                MaxConnections = Number("MaxConnections", 100, 2, 1_000_000),
                ConnectionWindowSeconds = Number("ConnectionWindowSeconds", 10, 1, 3600),
                MaxProbes = Number("MaxProbes", 3, 1, 1000),
                ProbeWindowMinutes = Number("ProbeWindowMinutes", 10, 1, 1440),
                ProbePaths = probes.Select(o => o.ToLowerInvariant()).Distinct().ToList(),
                Allow = allow,
                AllowPrivate = Flag("AllowPrivate", true),
                TrustedProxies = trusted
            };

            result._allowRanges = Ranges(allow, "Allow", errors, includeCloudflare: false);
            result._trustedRanges = Ranges(trusted, "TrustedProxies", errors, includeCloudflare: true);

            return result;
        }

        private static List<AddressRange> Ranges(IEnumerable<string> texts, string setting, List<string> problems, bool includeCloudflare)
        {
            var ranges = new List<AddressRange>();

            foreach (var text in texts)
            {
                if (includeCloudflare && text.Equals("cloudflare", StringComparison.OrdinalIgnoreCase))
                {
                    ranges.AddRange(AddressRange.Cloudflare);
                }
                else if (AddressRange.TryParse(text, out var range))
                {
                    ranges.Add(range!);
                }
                else
                {
                    problems.Add($"'{text}' in the setting {setting} is not an address or a range of addresses, so it is left out.");
                }
            }

            return ranges;
        }

        /// <summary>
        /// True for an address that is never banned.
        /// </summary>
        public bool IsAllowed(IPAddress address)
            => (AllowPrivate && AddressRange.Private.Any(o => o.Contains(address))) || _allowRanges.Any(o => o.Contains(address));

        /// <summary>
        /// True for a proxy that this NetProxy is behind, which can be believed about who its client is.
        /// </summary>
        public bool IsTrustedProxy(IPAddress address) => _trustedRanges.Any(o => o.Contains(address));

        public bool HasTrustedProxies => _trustedRanges.Count > 0;

        /// <summary>
        /// The probe pattern that the path matches, or null. The path is lower case and has no query string.
        /// </summary>
        public string? MatchProbe(string path)
        {
            foreach (var probe in ProbePaths)
            {
                if (probe.StartsWith('*') ? path.Contains(probe[1..], StringComparison.Ordinal) : path.StartsWith(probe, StringComparison.Ordinal))
                {
                    return probe.TrimStart('*');
                }
            }

            return null;
        }
    }
}
