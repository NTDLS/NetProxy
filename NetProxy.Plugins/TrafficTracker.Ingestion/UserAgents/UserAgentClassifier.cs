namespace NetProxy.TrafficTracker.Ingestion.UserAgents
{
    /// <summary>
    /// What a user agent is, in the few terms that a traffic report wants: the browser, the operating system, and what kind of client it is.
    /// This is a classifier for reports, not a validator: the text is whatever the client said it is, and it is stored too.
    /// </summary>
    public sealed record UserAgentInfo(string? Browser, string? OperatingSystem, string DeviceType, bool IsBot);

    public static class UserAgentClassifier
    {
        public const string Desktop = "Desktop";
        public const string Mobile = "Mobile";
        public const string Tablet = "Tablet";
        public const string Bot = "Bot";
        public const string Tool = "Tool";
        public const string Unknown = "Unknown";

        //Search engines, crawlers and monitors: the visitors that are not people.
        private static readonly string[] _botMarkers =
        [
            "bot", "crawler", "spider", "slurp", "crawl", "facebookexternalhit", "bingpreview", "headlesschrome", "lighthouse",
            "pingdom", "uptimerobot", "statuscake", "monitor", "archive.org", "mediapartners", "feedfetcher", "ahrefs", "semrush"
        ];

        //Programs and libraries that make requests: scripts, API clients, health checks.
        private static readonly (string Marker, string Name)[] _tools =
        [
            ("curl/", "curl"), ("wget/", "Wget"), ("python-requests", "python-requests"), ("python-urllib", "python-urllib"),
            ("go-http-client", "Go HTTP client"), ("postmanruntime", "Postman"), ("insomnia", "Insomnia"), ("okhttp", "OkHttp"),
            ("java/", "Java"), ("apache-httpclient", "Apache HttpClient"), ("libwww-perl", "libwww-perl"), ("axios/", "axios"),
            ("node-fetch", "node-fetch"), ("httpie", "HTTPie"), ("scrapy", "Scrapy"), ("aiohttp", "aiohttp"), ("restsharp", "RestSharp"),
            ("dotnet", ".NET HttpClient"), ("winhttp", "WinHTTP")
        ];

        public static UserAgentInfo Classify(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return new UserAgentInfo(null, null, Unknown, false);
            }

            var ua = userAgent;
            var os = OperatingSystemOf(ua);

            if (Contains(ua, _botMarkers))
            {
                return new UserAgentInfo(BotName(ua), os, Bot, true);
            }

            foreach (var (marker, name) in _tools)
            {
                if (ua.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return new UserAgentInfo(name, os, Tool, false);
                }
            }

            var browser = BrowserOf(ua);

            string device;
            if (ua.Contains("iPad", StringComparison.Ordinal) || ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
                || (ua.Contains("Android", StringComparison.Ordinal) && ua.Contains("Mobile", StringComparison.Ordinal) == false))
            {
                device = Tablet;
            }
            else if (ua.Contains("Mobile", StringComparison.Ordinal) || ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPod", StringComparison.Ordinal))
            {
                device = Mobile;
            }
            else
            {
                device = browser != null || os != null ? Desktop : Unknown;
            }

            return new UserAgentInfo(browser, os, device, false);
        }

        private static bool Contains(string text, string[] markers)
        {
            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BotName(string ua)
        {
            foreach (var (marker, name) in new[]
            {
                ("googlebot", "Googlebot"), ("bingbot", "Bingbot"), ("bingpreview", "Bing Preview"), ("slurp", "Yahoo Slurp"),
                ("duckduckbot", "DuckDuckBot"), ("baiduspider", "Baidu Spider"), ("yandex", "YandexBot"), ("facebookexternalhit", "Facebook"),
                ("ahrefs", "AhrefsBot"), ("semrush", "SemrushBot"), ("uptimerobot", "UptimeRobot"), ("pingdom", "Pingdom"),
                ("lighthouse", "Lighthouse"), ("headlesschrome", "Headless Chrome")
            })
            {
                if (ua.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            return "Bot";
        }

        private static string? BrowserOf(string ua)
        {
            //The order matters: most browsers say they are Chrome, and Chrome says it is Safari.
            if (ua.Contains("Edg/", StringComparison.Ordinal) || ua.Contains("EdgA/", StringComparison.Ordinal) || ua.Contains("EdgiOS/", StringComparison.Ordinal)
                || ua.Contains("Edge/", StringComparison.Ordinal))
            {
                return "Edge";
            }
            if (ua.Contains("OPR/", StringComparison.Ordinal) || ua.Contains("Opera", StringComparison.Ordinal))
            {
                return "Opera";
            }
            if (ua.Contains("SamsungBrowser/", StringComparison.Ordinal))
            {
                return "Samsung Internet";
            }
            if (ua.Contains("UCBrowser/", StringComparison.Ordinal))
            {
                return "UC Browser";
            }
            if (ua.Contains("Firefox/", StringComparison.Ordinal) || ua.Contains("FxiOS/", StringComparison.Ordinal))
            {
                return "Firefox";
            }
            if (ua.Contains("Chrome/", StringComparison.Ordinal) || ua.Contains("CriOS/", StringComparison.Ordinal) || ua.Contains("Chromium/", StringComparison.Ordinal))
            {
                return "Chrome";
            }
            if (ua.Contains("MSIE ", StringComparison.Ordinal) || ua.Contains("Trident/", StringComparison.Ordinal))
            {
                return "Internet Explorer";
            }
            if (ua.Contains("Safari/", StringComparison.Ordinal) && ua.Contains("Version/", StringComparison.Ordinal))
            {
                return "Safari";
            }

            return null;
        }

        private static string? OperatingSystemOf(string ua)
        {
            //iOS says it is a Mac, and Android says it is Linux, so those come first.
            if (ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) || ua.Contains("iPod", StringComparison.Ordinal))
            {
                return "iOS";
            }
            if (ua.Contains("Android", StringComparison.Ordinal))
            {
                return "Android";
            }
            if (ua.Contains("Windows", StringComparison.Ordinal))
            {
                return "Windows";
            }
            if (ua.Contains("CrOS", StringComparison.Ordinal))
            {
                return "ChromeOS";
            }
            if (ua.Contains("Mac OS X", StringComparison.Ordinal) || ua.Contains("Macintosh", StringComparison.Ordinal))
            {
                return "macOS";
            }
            if (ua.Contains("Linux", StringComparison.Ordinal) || ua.Contains("X11", StringComparison.Ordinal))
            {
                return "Linux";
            }

            return null;
        }
    }
}
