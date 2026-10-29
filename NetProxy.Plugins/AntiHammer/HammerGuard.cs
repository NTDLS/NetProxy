using NetProxy.Plugins.Common;
using System.Net;

namespace NetProxy.Plugins.AntiHammer
{
    /// <summary>
    /// Decides who gets banned. It is told about the connections and requests of an address, counts them, and says when a limit is passed.
    /// It does not touch connections: what to do with a ban is up to the plug-in, which is what keeps this testable.
    /// </summary>
    internal sealed class HammerGuard
    {
        private readonly AntiHammerSettings _settings;
        private readonly TimeProvider _clock;

        public HammerGuard(AntiHammerSettings settings, TimeProvider clock)
        {
            _settings = settings;
            _clock = clock;
            Bans = new BanList(settings, clock);
            Rates = new RateTracker(clock);
        }

        public BanList Bans { get; }
        public RateTracker Rates { get; }

        /// <summary>
        /// The ban that is in force for the address, or null. An address that is allowed is never banned, whatever was saved.
        /// </summary>
        public BanEntry? FindBan(IPAddress address)
            => _settings.IsAllowed(address) ? null : Bans.GetActive(AddressRange.KeyOf(address));

        /// <summary>
        /// A client connected. Returns the ban if that was one too many.
        /// </summary>
        public BanEntry? CountConnection(IPAddress address)
        {
            var key = AddressRange.KeyOf(address);
            var window = TimeSpan.FromSeconds(_settings.ConnectionWindowSeconds);

            return Rates.Hit($"c|{key}", window) > _settings.MaxConnections
                ? BanFor(key, $"more than {_settings.MaxConnections} connections in {_settings.ConnectionWindowSeconds} seconds")
                : null;
        }

        /// <summary>
        /// A client asked for a path. Returns the ban if that was one too many of anything.
        /// </summary>
        /// <param name="target">The request target as it was sent: the path, and the query string if there is one.</param>
        public BanEntry? CountRequest(IPAddress address, string target)
        {
            var key = AddressRange.KeyOf(address);
            var path = PathOf(target);

            if (_settings.MatchProbe(path) is { } probe
                && Rates.Hit($"x|{key}", TimeSpan.FromMinutes(_settings.ProbeWindowMinutes)) >= _settings.MaxProbes)
            {
                return BanFor(key, $"looking for pages that are not there ({probe}), {_settings.MaxProbes} times in {_settings.ProbeWindowMinutes} minutes");
            }

            if (Rates.Hit($"r|{key}", TimeSpan.FromSeconds(_settings.RequestWindowSeconds)) > _settings.MaxRequests)
            {
                return BanFor(key, $"more than {_settings.MaxRequests} requests in {_settings.RequestWindowSeconds} seconds");
            }

            if (Rates.Hit($"p|{key}|{path}", TimeSpan.FromSeconds(_settings.SamePathWindowSeconds)) > _settings.MaxSamePath)
            {
                return BanFor(key, $"more than {_settings.MaxSamePath} requests for {Shorten(path)} in {_settings.SamePathWindowSeconds} seconds");
            }

            return null;
        }

        private BanEntry BanFor(string key, string reason)
        {
            var ban = Bans.Ban(key, reason);
            Rates.Forget(key);
            return ban;
        }

        /// <summary>
        /// The path of a request target, in lower case and without the query string.
        /// </summary>
        public static string PathOf(string target)
        {
            //The absolute form: "GET http://example.com/path HTTP/1.1".
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var slash = target.IndexOf('/', target.IndexOf("//", StringComparison.Ordinal) + 2);
                target = slash < 0 ? "/" : target[slash..];
            }

            var end = target.IndexOfAny(['?', '#']);
            return (end < 0 ? target : target[..end]).ToLowerInvariant();
        }

        private static string Shorten(string text) => text.Length <= 80 ? text : text[..80] + "...";

        /// <summary>
        /// Drops what is of no use any more.
        /// </summary>
        public void Maintain()
        {
            Rates.Purge(_settings.LongestWindow + TimeSpan.FromSeconds(5));
            Bans.Purge();
        }

        public TimeProvider Clock => _clock;
    }
}
