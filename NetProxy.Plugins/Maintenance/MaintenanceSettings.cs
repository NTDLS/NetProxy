using NetProxy.Plugins.Common;
using System.Net;

namespace NetProxy.Plugins.Maintenance
{
    /// <summary>
    /// What the plug-in is set up with: the name=value lines of its settings in the NetProxy console. Every setting has a default, so a
    /// plug-in with no settings at all answers with a plain "we'll be right back" page.
    /// </summary>
    public sealed class MaintenanceSettings
    {
        /// <summary>The status of the response: 503 says "come back later" to people and to search engines. Setting: StatusCode</summary>
        public int StatusCode { get; init; } = 503;

        /// <summary>Tells browsers and crawlers when to try again; 0 leaves the header out. Setting: RetryAfterSeconds</summary>
        public int RetryAfterSeconds { get; init; } = 120;

        /// <summary>The heading of the page. Setting: Title</summary>
        public string Title { get; init; } = "We'll be right back";

        /// <summary>The text of the page. Setting: Message</summary>
        public string Message { get; init; } = "This site is down for maintenance. Please try again in a few minutes.";

        /// <summary>
        /// An HTML file to answer with instead of the page that is built from Title and Message. Relative to the data folder of the plug-in
        /// when it is not a full path. It is read again when it changes, so it can be edited while the service runs. Setting: PageFile
        /// </summary>
        public string? PageFile { get; init; }

        /// <summary>The type of the page, for a PageFile that is not HTML. Setting: ContentType</summary>
        public string ContentType { get; init; } = "text/html; charset=utf-8";

        /// <summary>
        /// Answer every request with the page, also when the endpoints work. For planned maintenance: the site is switched off by switching
        /// this on. Setting: Force
        /// </summary>
        public bool Force { get; init; }

        /// <summary>When the page switches on by itself. Settings: Window1, Weekly1, Daily1 and TimeZone</summary>
        internal MaintenanceSchedule Schedule { get; init; } = new();

        /// <summary>Addresses and ranges that still get to the site while Force is on ("203.0.113.0/24"), comma separated. Setting: Allow</summary>
        public IReadOnlyList<string> Allow { get; init; } = [];

        private IReadOnlyList<AddressRange> _allowRanges = [];

        public static MaintenanceSettings From(IReadOnlyDictionary<string, string> settings, out List<string> problems)
        {
            problems = [];
            var found = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            var errors = problems;

            string? Text(string name) => found.TryGetValue(name, out var value) && string.IsNullOrWhiteSpace(value) == false ? value.Trim() : null;

            int Number(string name, int fallback, int min, int max)
            {
                if (Text(name) is { } value)
                {
                    if (int.TryParse(value, out var number) && number >= min && number <= max)
                    {
                        return number;
                    }

                    errors.Add($"The setting {name} must be a number from {min} to {max}; '{value}' is not, so {fallback} is used.");
                }

                return fallback;
            }

            var force = false;
            if (Text("Force") is { } forceText)
            {
                if (bool.TryParse(forceText, out force) == false)
                {
                    errors.Add($"The setting Force must be true or false; '{forceText}' is not, so false is used.");
                    force = false;
                }
            }

            var allow = (Text("Allow") ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var ranges = new List<AddressRange>();
            foreach (var text in allow)
            {
                if (AddressRange.TryParse(text, out var range))
                {
                    ranges.Add(range!);
                }
                else
                {
                    errors.Add($"'{text}' in the setting Allow is not an address or a range of addresses, so it is left out.");
                }
            }

            var reader = new SettingsReader(settings);
            var schedule = MaintenanceSchedule.Parse(reader.Numbered("Window"), reader.Numbered("Weekly"), reader.Numbered("Daily"), Text("TimeZone"), errors);

            return new MaintenanceSettings
            {
                Schedule = schedule,
                StatusCode = Number("StatusCode", 503, 400, 599),
                RetryAfterSeconds = Number("RetryAfterSeconds", 120, 0, 86_400 * 30),
                Title = Text("Title") ?? "We'll be right back",
                Message = Text("Message") ?? "This site is down for maintenance. Please try again in a few minutes.",
                PageFile = Text("PageFile"),
                ContentType = Text("ContentType") ?? "text/html; charset=utf-8",
                Force = force,
                Allow = allow,
                _allowRanges = ranges
            };
        }

        public bool IsAllowed(IPAddress address) => _allowRanges.Any(o => o.Contains(address));
    }
}
