using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Text;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Chooses which of the proxies that share a port gets a connection, by the host name that the client asks for.
    /// </summary>
    internal static class NpHostRouter
    {
        /// <summary>
        /// The most of an HTTP request that is read to find the Host header.
        /// </summary>
        public const int MaxHeaderBytes = 64 * 1024;

        /// <summary>
        /// The proxy for a host name: the one with the most specific matching name (an exact name before a wildcard, a longer
        /// wildcard before a shorter one), otherwise the default proxy (the one without host names). Null if there is neither.
        /// </summary>
        public static NpProxy? Match(IEnumerable<NpProxy> routes, string? host)
        {
            var normalized = NpHostMatcher.Normalize(host);
            var candidates = routes.ToList();

            if (normalized != null)
            {
                NpProxy? bestProxy = null;
                string? bestPattern = null;

                foreach (var proxy in candidates)
                {
                    var pattern = NpHostMatcher.FindBest(proxy.HostPatterns, normalized);
                    if (pattern != null && (bestPattern == null || NpHostMatcher.Specificity(pattern) > NpHostMatcher.Specificity(bestPattern)))
                    {
                        bestProxy = proxy;
                        bestPattern = pattern;
                    }
                }

                if (bestProxy != null)
                {
                    return bestProxy;
                }
            }

            return candidates.FirstOrDefault(o => o.HostPatterns.Count == 0);
        }

        /// <summary>
        /// Reads the Host header from the start of an HTTP/1.x request. Returns false while the header is not complete yet.
        /// The host is null when the request has none.
        /// </summary>
        public static bool TryReadHost(ReadOnlySpan<byte> data, out string? host)
        {
            host = null;

            var end = data.IndexOf("\r\n\r\n"u8);
            if (end < 0)
            {
                return false;
            }

            var lines = Encoding.Latin1.GetString(data[..end]).Split("\r\n");

            //The first line is the request line.
            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && line.AsSpan(0, colon).Trim().Equals("host", StringComparison.OrdinalIgnoreCase))
                {
                    host = NpHostMatcher.Normalize(line[(colon + 1)..]);
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Why a proxy cannot share its port with the others, or null if it can. Only the proxies that use the same port (and
        /// overlapping addresses) matter. Used when a proxy is saved, and when it starts.
        /// </summary>
        public static string? FindSharingConflict(NpProxyConfiguration candidate, IEnumerable<NpProxyConfiguration> others)
        {
            foreach (var other in others)
            {
                if (other.Id == candidate.Id || other.ListenPort != candidate.ListenPort || BindingsOverlap(candidate, other) == false)
                {
                    continue;
                }

                var inUse = $"Port {candidate.ListenPort} is already used by proxy '{other.Name}'.";

                if (candidate.TrafficType == TrafficType.Raw || other.TrafficType == TrafficType.Raw)
                {
                    return $"{inUse} Only HTTP and HTTPS proxies can share a port (a RAW proxy has no host name to tell it apart by).";
                }

                if (candidate.TrafficType != other.TrafficType)
                {
                    return $"{inUse} Proxies can only share a port if they are all HTTP or all HTTPS.";
                }

                var mine = Patterns(candidate);
                var theirs = Patterns(other);

                if (mine.Count == 0 && theirs.Count == 0)
                {
                    return $"{inUse} To share a port, give each proxy host names (at most one proxy per port can have none, and be the default).";
                }

                var duplicate = mine.Intersect(theirs).FirstOrDefault();
                if (duplicate != null)
                {
                    return $"{inUse} Both answer to the host name '{duplicate}'.";
                }
            }

            return null;
        }

        /// <summary>
        /// The normalized names of the enabled host names of a proxy.
        /// </summary>
        public static HashSet<string> Patterns(NpProxyConfiguration configuration)
            => configuration.HostNames
                .Where(o => o.Enabled)
                .Select(o => NpHostMatcher.Normalize(o.Host))
                .OfType<string>()
                .ToHashSet();

        private static bool BindingsOverlap(NpProxyConfiguration a, NpProxyConfiguration b)
        {
            if (a.ListenOnAllAddresses || b.ListenOnAllAddresses)
            {
                return true;
            }

            var addresses = a.Bindings.Where(o => o.Enabled).Select(o => NpNetwork.NormalizeHost(o.Address).ToLowerInvariant()).ToHashSet();
            return b.Bindings.Where(o => o.Enabled).Any(o => addresses.Contains(NpNetwork.NormalizeHost(o.Address).ToLowerInvariant()));
        }

        /// <summary>
        /// Cleans up the host names of a proxy that is being saved (normalized, no blanks), and returns why they cannot be used, if so.
        /// </summary>
        public static string? NormalizeAndValidate(NpProxyConfiguration configuration)
        {
            var hosts = new List<NpHostName>();

            foreach (var entry in configuration.HostNames)
            {
                if (string.IsNullOrWhiteSpace(entry.Host))
                {
                    continue;
                }

                var host = NpHostMatcher.Normalize(entry.Host);
                if (NpHostMatcher.IsValidPattern(host) == false)
                {
                    return $"'{entry.Host}' is not a valid host name (use a name like www.example.com, or *.example.com for all names below a domain).";
                }

                if (hosts.Any(o => o.Host == host))
                {
                    return $"The host name '{host}' is listed more than once.";
                }

                entry.Host = host!;
                entry.TlsCertificatePath = entry.TlsCertificatePath?.Trim() ?? string.Empty;
                hosts.Add(entry);
            }

            configuration.HostNames = hosts;
            return null;
        }
    }
}
