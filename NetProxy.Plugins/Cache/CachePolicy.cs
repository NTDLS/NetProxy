using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.Cache
{
    /// <summary>
    /// What may be kept and for how long. A shared cache has to be careful: what is kept for one client is given to the next, so
    /// anything that could be about one client (a login, a cookie, a response that says it is private) is never kept.
    /// </summary>
    internal sealed class CachePolicy
    {
        private static readonly string[] BypassHeaders = ["Authorization", "Range", "If-Range", "If-None-Match", "If-Modified-Since", "If-Match", "If-Unmodified-Since", "Upgrade"];

        /// <summary>How long a response is kept when the endpoint does not say. Setting: DefaultTtlSeconds</summary>
        public int DefaultTtlSeconds { get; init; } = 60;

        /// <summary>The longest a response is kept, whatever the endpoint says. Setting: MaxTtlSeconds</summary>
        public int MaxTtlSeconds { get; init; } = 86_400;

        /// <summary>Responses bigger than this are not kept. Setting: MaxObjectKB</summary>
        public int MaxObjectBytes { get; init; } = 512 * 1024;

        /// <summary>The statuses that are kept. Setting: Statuses</summary>
        public HashSet<int> Statuses { get; init; } = [200, 301, 308];

        /// <summary>Only these hosts (empty: all). Setting: Hosts</summary>
        public IReadOnlyList<string> Hosts { get; init; } = [];

        /// <summary>Only paths that start with one of these (empty: all). Setting: Paths</summary>
        public IReadOnlyList<string> Paths { get; init; } = [];

        /// <summary>Never paths that start with one of these. Setting: ExcludePaths</summary>
        public IReadOnlyList<string> ExcludePaths { get; init; } = [];

        /// <summary>Requests with a cookie are kept and served too. Off, a cookie means the response may be for one client. Setting: AllowCookies</summary>
        public bool AllowCookies { get; init; }

        /// <summary>Only responses that say how long they can be kept (max-age) are kept. Setting: RequireOriginTtl</summary>
        public bool RequireOriginTtl { get; init; }

        /// <summary>A request that says no-cache or max-age=0 (a reload) goes to the endpoint. Setting: HonorRequestCacheControl</summary>
        public bool HonorRequestCacheControl { get; init; }

        public static CachePolicy From(SettingsReader settings)
        {
            var statuses = new HashSet<int>();
            foreach (var text in settings.Has("Statuses") ? settings.List("Statuses") : ["200", "301", "308"])
            {
                if (int.TryParse(text, out var status) && status is >= 200 and <= 599 && status != 206)
                {
                    statuses.Add(status);
                }
                else
                {
                    settings.Problems.Add($"'{text}' in the setting Statuses is not a status that can be kept, so it is left out.");
                }
            }

            return new CachePolicy
            {
                DefaultTtlSeconds = settings.Number("DefaultTtlSeconds", 60, 0, 31_536_000),
                MaxTtlSeconds = settings.Number("MaxTtlSeconds", 86_400, 1, 31_536_000),
                MaxObjectBytes = settings.Number("MaxObjectKB", 512, 1, 102_400) * 1024,
                Statuses = statuses,
                Hosts = settings.List("Hosts").Select(o => o.ToLowerInvariant()).ToList(),
                Paths = settings.List("Paths"),
                ExcludePaths = settings.List("ExcludePaths"),
                AllowCookies = settings.Flag("AllowCookies", false),
                RequireOriginTtl = settings.Flag("RequireOriginTtl", false),
                HonorRequestCacheControl = settings.Flag("HonorRequestCacheControl", false)
            };
        }

        /// <summary>
        /// The key of the response to a request, or null when the request is not for the cache (it is not kept, and none is served).
        /// </summary>
        public string? KeyFor(SeenRequest request, string scheme)
        {
            if (request.Method != "GET" || request.BodyPending || request.Host == null)
            {
                return null;
            }

            if (BypassHeaders.Any(o => request.Header(o) != null) || (AllowCookies == false && request.Header("Cookie") != null))
            {
                return null;
            }

            if (HonorRequestCacheControl && (Directives(request.Header("Cache-Control")).Any(o => o.Name is "no-cache" or "no-store" || (o.Name == "max-age" && o.Value == "0"))
                || request.Header("Pragma")?.Contains("no-cache", StringComparison.OrdinalIgnoreCase) == true))
            {
                return null;
            }

            var host = ExchangeTracker.NormalizeHost(request.Host) ?? string.Empty;
            var path = request.Path;

            if ((Hosts.Count > 0 && Hosts.Any(o => o.StartsWith("*.") ? host.EndsWith(o[1..], StringComparison.Ordinal) : host == o) == false)
                || (Paths.Count > 0 && Paths.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase)) == false)
                || ExcludePaths.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            //The same URL can be answered differently to clients that take different encodings, so the encoding is part of the key.
            return $"{scheme}://{request.Host.ToLowerInvariant()}{request.Path}{request.Query}|{request.Header("Accept-Encoding")?.Trim().ToLowerInvariant()}";
        }

        /// <summary>
        /// For how long a response can be kept, or null when it can not be.
        /// </summary>
        public TimeSpan? TtlFor(HttpResponseHead head, int bodyLength)
        {
            if (Statuses.Contains(head.Status) == false || head.ContentLength != bodyLength || bodyLength > MaxObjectBytes
                || head.Has("Set-Cookie") || head.Has("Transfer-Encoding") || head.Has("Content-Range"))
            {
                return null;
            }

            //Only a difference in the encoding is accounted for in the key (see KeyFor): any other Vary could not be told apart.
            if (head.GetAll("Vary").SelectMany(o => o.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Any(o => o.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) == false))
            {
                return null;
            }

            if (head.Get("Pragma")?.Contains("no-cache", StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }

            int? maxAge = null, sharedMaxAge = null;

            foreach (var (name, value) in head.GetAll("Cache-Control").SelectMany(o => Directives(o)))
            {
                switch (name)
                {
                    case "no-store" or "no-cache" or "private":
                        return null;
                    case "max-age":
                        maxAge = int.TryParse(value, out var age) ? age : null;
                        break;
                    case "s-maxage":
                        sharedMaxAge = int.TryParse(value, out var shared) ? shared : null;
                        break;
                }
            }

            var seconds = sharedMaxAge ?? maxAge;
            if (seconds == null)
            {
                if (RequireOriginTtl)
                {
                    return null;
                }

                seconds = DefaultTtlSeconds;
            }

            seconds = Math.Min(seconds.Value, MaxTtlSeconds);
            return seconds > 0 ? TimeSpan.FromSeconds(seconds.Value) : null;
        }

        internal static IEnumerable<(string Name, string? Value)> Directives(string? header)
        {
            foreach (var part in (header ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = part.IndexOf('=');
                yield return equals < 0
                    ? (part.ToLowerInvariant(), null)
                    : (part[..equals].Trim().ToLowerInvariant(), part[(equals + 1)..].Trim().Trim('"'));
            }
        }
    }
}
