using NetProxy.Plugins.Common.TrafficTracker;

namespace NetProxy.Plugins.TrafficTracker
{
    /// <summary>
    /// What the plug-in is set up with: the name=value lines of its settings in the NetProxy console. Every setting has a default, so a
    /// plug-in with no settings at all sends what it sees to a CatMQ server on this machine.
    /// </summary>
    public sealed class TrackerSettings
    {
        /// <summary>The CatMQ server. Setting: Mq.Host</summary>
        public string MqHost { get; init; } = "localhost";

        /// <summary>Setting: Mq.Port</summary>
        public int MqPort { get; init; } = TrafficQueue.DefaultPort;

        /// <summary>Setting: Mq.Queue</summary>
        public string Queue { get; init; } = TrafficQueue.DefaultName;

        /// <summary>Which NetProxy this is, for when several send to one queue. Setting: Source (default: the machine name)</summary>
        public string Source { get; init; } = Environment.MachineName;

        /// <summary>How many requests go in one message. Setting: BatchSize</summary>
        public int BatchSize { get; init; } = 200;

        /// <summary>How long a request waits for the others before it is sent. Setting: FlushIntervalMs</summary>
        public int FlushIntervalMs { get; init; } = 1000;

        /// <summary>
        /// How many requests are kept while the queue cannot be reached. The oldest go first, because the traffic of a proxy must never be
        /// held up (or its memory used up) for the sake of statistics. Setting: MaxQueued
        /// </summary>
        public int MaxQueued { get; init; } = 50_000;

        /// <summary>Keep the response (status, size, how long it took) with the request. Setting: RecordResponses</summary>
        public bool RecordResponses { get; init; } = true;

        /// <summary>Keep the query string of the requests. It can hold things that people would not want kept. Setting: RecordQueryStrings</summary>
        public bool RecordQueryStrings { get; init; }

        /// <summary>Keep the Referer header (without its query string unless RecordQueryStrings is on). Setting: RecordReferrers</summary>
        public bool RecordReferrers { get; init; } = true;

        /// <summary>
        /// Use the first address of X-Forwarded-For as the client, for a NetProxy that is behind another proxy. Only turn it on when that is
        /// so: anyone can send the header. Setting: TrustForwardedFor
        /// </summary>
        public bool TrustForwardedFor { get; init; }

        /// <summary>Also follow TCP proxies, for the ones that carry plain HTTP. Setting: IncludeTcpProxies</summary>
        public bool IncludeTcpProxies { get; init; }

        /// <summary>Requests for these hosts are not kept: a comma separated list, with *.example.com for everything below a domain. Setting: IgnoreHosts</summary>
        public IReadOnlyList<string> IgnoreHosts { get; init; } = [];

        /// <summary>Requests for paths that start with these are not kept (a comma separated list, "/health" for example). Setting: IgnorePathPrefixes</summary>
        public IReadOnlyList<string> IgnorePathPrefixes { get; init; } = [];

        /// <summary>Requests from these client addresses are not kept (a comma separated list). Setting: IgnoreClients</summary>
        public IReadOnlyList<string> IgnoreClients { get; init; } = [];

        public static TrackerSettings From(IReadOnlyDictionary<string, string> settings, out List<string> problems)
        {
            problems = [];
            var found = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            var errors = problems;

            string Text(string name, string fallback)
                => found.TryGetValue(name, out var value) && string.IsNullOrWhiteSpace(value) == false ? value.Trim() : fallback;

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

            IReadOnlyList<string> List(string name)
                => found.TryGetValue(name, out var value)
                    ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : [];

            var queue = Text("Mq.Queue", TrafficQueue.DefaultName);
            if (NTDLS.CatMQ.Shared.CMqQueueConfiguration.IsValidQueueName(queue) == false)
            {
                problems.Add($"'{queue}' is not a valid queue name, so {TrafficQueue.DefaultName} is used.");
                queue = TrafficQueue.DefaultName;
            }

            return new TrackerSettings
            {
                MqHost = Text("Mq.Host", "localhost"),
                MqPort = Number("Mq.Port", TrafficQueue.DefaultPort, 1, 65535),
                Queue = queue,
                Source = Text("Source", Environment.MachineName),
                BatchSize = Number("BatchSize", 200, 1, 5000),
                FlushIntervalMs = Number("FlushIntervalMs", 1000, 50, 60_000),
                MaxQueued = Number("MaxQueued", 50_000, 100, 5_000_000),
                RecordResponses = Flag("RecordResponses", true),
                RecordQueryStrings = Flag("RecordQueryStrings", false),
                RecordReferrers = Flag("RecordReferrers", true),
                TrustForwardedFor = Flag("TrustForwardedFor", false),
                IncludeTcpProxies = Flag("IncludeTcpProxies", false),
                IgnoreHosts = List("IgnoreHosts"),
                IgnorePathPrefixes = List("IgnorePathPrefixes"),
                IgnoreClients = List("IgnoreClients")
            };
        }

        /// <summary>
        /// True when the host is one of the ignored ones: an exact name, or a wildcard ("*.example.com") for the names below a domain.
        /// </summary>
        public bool IsIgnoredHost(string host)
        {
            foreach (var pattern in IgnoreHosts)
            {
                if (pattern.StartsWith("*.", StringComparison.Ordinal)
                    ? host.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
                    : host.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsIgnoredPath(string path)
            => IgnorePathPrefixes.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase));

        public bool IsIgnoredClient(string address)
            => IgnoreClients.Any(o => o.Equals(address, StringComparison.OrdinalIgnoreCase));
    }
}
