using System.Globalization;

namespace NetProxy.Plugins.Maintenance
{
    /// <summary>
    /// When the site is down for maintenance by plan: windows that switch the page on by themselves, once or again and again.
    ///
    ///   Window1=2026-11-02 01:00 to 2026-11-02 03:00      once
    ///   Weekly1=Sun 02:00-04:00                            every Sunday (Sat,Sun for two days)
    ///   Daily1=03:00-03:30                                 every day
    ///
    /// The times are in the time zone of the setting TimeZone (an id such as "Europe/Berlin" or "Eastern Standard Time"), UTC if there is
    /// none. A window that ends before it starts (22:00-02:00) goes over midnight.
    /// </summary>
    internal sealed class MaintenanceSchedule
    {
        private sealed record Window(DateTime? FromUtc, DateTime? ToUtc, HashSet<DayOfWeek>? Days, TimeSpan From, TimeSpan To, string Text);

        private readonly List<Window> _windows = [];
        private TimeZoneInfo _zone = TimeZoneInfo.Utc;

        public bool IsEmpty => _windows.Count == 0;

        public static MaintenanceSchedule Parse(IEnumerable<(string Name, string Value)> windows, IEnumerable<(string Name, string Value)> weekly,
            IEnumerable<(string Name, string Value)> daily, string? timeZone, List<string> problems)
        {
            var schedule = new MaintenanceSchedule();

            if (timeZone != null)
            {
                try
                {
                    schedule._zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    problems.Add($"'{timeZone}' in the setting TimeZone is not a time zone of this machine, so UTC is used.");
                }
            }

            foreach (var (name, value) in windows)
            {
                var parts = value.Split([" to ", ".."], StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) == false
                    || DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) == false || to <= from)
                {
                    problems.Add($"{name} is left out: a window is 'start to end', for example '2026-11-02 01:00 to 2026-11-02 03:00', and ends after it starts.");
                    continue;
                }

                try
                {
                    schedule._windows.Add(new Window(schedule.ToUtc(from), schedule.ToUtc(to), null, default, default, value));
                }
                catch (ArgumentException)
                {
                    problems.Add($"{name} is left out: '{value}' is a time that does not exist in the time zone (the clocks go forward).");
                }
            }

            foreach (var (name, value) in weekly)
            {
                var space = value.LastIndexOf(' '); //The times are the last word, so the days can have spaces after their commas.
                var days = space > 0 ? ParseDays(value[..space]) : null;

                if (days == null || TryTimes(value[(space + 1)..], out var from, out var to) == false)
                {
                    problems.Add($"{name} is left out: a weekly window is 'days from-to', for example 'Sun 02:00-04:00' or 'Sat,Sun 22:00-02:00'.");
                    continue;
                }

                schedule._windows.Add(new Window(null, null, days, from, to, value));
            }

            foreach (var (name, value) in daily)
            {
                if (TryTimes(value, out var from, out var to) == false)
                {
                    problems.Add($"{name} is left out: a daily window is 'from-to', for example '03:00-03:30'.");
                    continue;
                }

                schedule._windows.Add(new Window(null, null, null, from, to, value));
            }

            return schedule;
        }

        private DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zone);

        private static HashSet<DayOfWeek>? ParseDays(string text)
        {
            var days = new HashSet<DayOfWeek>();

            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var day = Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>().FirstOrDefault(o => o.ToString()!.StartsWith(part, StringComparison.OrdinalIgnoreCase) && part.Length >= 3);
                if (day == null)
                {
                    return null;
                }

                days.Add(day.Value);
            }

            return days.Count == 0 ? null : days;
        }

        private static bool TryTimes(string text, out TimeSpan from, out TimeSpan to)
        {
            to = default;
            var parts = text.Split('-', StringSplitOptions.TrimEntries);
            from = default;

            return parts.Length == 2 && TimeSpan.TryParseExact(parts[0], @"h\:mm", CultureInfo.InvariantCulture, out from)
                && TimeSpan.TryParseExact(parts[1], @"h\:mm", CultureInfo.InvariantCulture, out to) && from != to;
        }

        /// <summary>
        /// The time that the window we are in ends (the latest, if more than one), or null when we are in none.
        /// </summary>
        public DateTime? ActiveUntil(DateTime nowUtc)
        {
            DateTime? until = null;

            foreach (var window in _windows)
            {
                foreach (var (start, end) in Occurrences(window, nowUtc.AddDays(-1), nowUtc))
                {
                    if (start <= nowUtc && nowUtc < end && (until == null || end > until))
                    {
                        until = end;
                    }
                }
            }

            return until;
        }

        /// <summary>
        /// When the next window starts, or null if none does in the next week.
        /// </summary>
        public DateTime? NextStart(DateTime nowUtc)
            => _windows.SelectMany(o => Occurrences(o, nowUtc, nowUtc.AddDays(8))).Where(o => o.Start > nowUtc).Select(o => (DateTime?)o.Start).Min();

        /// <summary>The windows that overlap the span, as start and end in UTC.</summary>
        private IEnumerable<(DateTime Start, DateTime End)> Occurrences(Window window, DateTime fromUtc, DateTime toUtc)
        {
            if (window.FromUtc != null)
            {
                yield return (window.FromUtc.Value, window.ToUtc!.Value);
                yield break;
            }

            var firstDay = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, _zone).Date.AddDays(-1);
            var lastDay = TimeZoneInfo.ConvertTimeFromUtc(toUtc, _zone).Date;

            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                if (window.Days != null && window.Days.Contains(day.DayOfWeek) == false)
                {
                    continue;
                }

                var start = day + window.From;
                var end = day + window.To + (window.To <= window.From ? TimeSpan.FromDays(1) : TimeSpan.Zero);

                //A time that does not exist (the clocks go forward) is moved to the one after it.
                yield return (Convert(start), Convert(end));
            }
        }

        private DateTime Convert(DateTime local)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return _zone.IsInvalidTime(local) ? TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), _zone) : TimeZoneInfo.ConvertTimeToUtc(local, _zone);
        }

        public override string ToString() => string.Join("; ", _windows.Select(o => o.Text));
    }
}
