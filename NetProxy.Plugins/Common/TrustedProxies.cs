using System.Net;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// The proxies that this NetProxy is behind (Cloudflare, say). A request that comes through one of them says who the client is, in
    /// CF-Connecting-IP or X-Forwarded-For. What anyone else puts in those headers is not believed.
    /// </summary>
    internal sealed class TrustedProxies
    {
        private readonly List<AddressRange> _ranges = [];

        /// <param name="entries">Addresses and ranges, and "cloudflare" for the addresses of Cloudflare.</param>
        public TrustedProxies(IEnumerable<string> entries, List<string> problems, string settingName)
        {
            foreach (var entry in entries)
            {
                if (entry.Equals("cloudflare", StringComparison.OrdinalIgnoreCase))
                {
                    _ranges.AddRange(AddressRange.Cloudflare);
                }
                else if (AddressRange.TryParse(entry, out var range))
                {
                    _ranges.Add(range!);
                }
                else
                {
                    problems.Add($"'{entry}' in the setting {settingName} is not an address or a range of addresses, so it is left out.");
                }
            }
        }

        public bool Any => _ranges.Count > 0;

        public bool IsTrusted(IPAddress address) => _ranges.Any(o => o.Contains(address));

        /// <summary>
        /// The client of a request that arrived from <paramref name="peer"/>: the peer itself, unless that is a trusted proxy, and then
        /// CF-Connecting-IP if the request has one, otherwise the last address of X-Forwarded-For that is not a trusted proxy itself (the
        /// ones before it were sent by the client, which can send anything).
        /// </summary>
        public IPAddress Client(IPAddress peer, SeenRequest request) => Client(peer, request.ConnectingIp, request.ForwardedFor);

        public IPAddress Client(IPAddress peer, string? connectingIp, string? forwardedFor)
        {
            if (IsTrusted(peer) == false)
            {
                return peer;
            }

            if (AddressRange.TryParseAddress(connectingIp, out var connecting))
            {
                return connecting;
            }

            if (forwardedFor != null)
            {
                foreach (var entry in forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
                {
                    if (AddressRange.TryParseAddress(entry, out var address) == false)
                    {
                        break;
                    }

                    if (IsTrusted(address) == false)
                    {
                        return address;
                    }
                }
            }

            return peer;
        }
    }
}
