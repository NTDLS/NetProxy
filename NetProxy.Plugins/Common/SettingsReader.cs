namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// Reads the name=value settings of a plug-in. A value that is not usable is reported in <see cref="Problems"/> and the default is used,
    /// so a typo in the console never stops a plug-in from loading.
    /// </summary>
    internal sealed class SettingsReader
    {
        private readonly Dictionary<string, string> _found;

        public SettingsReader(IReadOnlyDictionary<string, string> settings) => _found = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);

        public List<string> Problems { get; } = [];

        public bool Has(string name) => _found.TryGetValue(name, out var value) && string.IsNullOrWhiteSpace(value) == false;

        public string? Text(string name) => Has(name) ? _found[name].Trim() : null;

        public string Text(string name, string fallback) => Text(name) ?? fallback;

        public int Number(string name, int fallback, int min, int max)
        {
            if (Text(name) is { } value)
            {
                if (int.TryParse(value, out var number) && number >= min && number <= max)
                {
                    return number;
                }

                Problems.Add($"The setting {name} must be a number from {min} to {max}; '{value}' is not, so {fallback} is used.");
            }

            return fallback;
        }

        public bool Flag(string name, bool fallback)
        {
            if (Text(name) is { } value)
            {
                if (bool.TryParse(value, out var flag))
                {
                    return flag;
                }

                Problems.Add($"The setting {name} must be true or false; '{value}' is not, so {fallback} is used.");
            }

            return fallback;
        }

        /// <summary>A comma (or semicolon, or line) separated list.</summary>
        public List<string> List(string name)
            => Text(name)?.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [];

        /// <summary>
        /// The settings whose name is the prefix and a number ("Rule1", "Rule2", ... "Rule10"), in the order of the numbers.
        /// </summary>
        public IEnumerable<(string Name, string Value)> Numbered(string prefix)
            => _found
                .Where(o => o.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(o.Value) == false
                    && (o.Key.Length == prefix.Length || int.TryParse(o.Key[prefix.Length..].TrimStart('.', '_'), out _)))
                .OrderBy(o => o.Key.Length == prefix.Length ? 0 : int.Parse(o.Key[prefix.Length..].TrimStart('.', '_')))
                .Select(o => (o.Key, o.Value.Trim()));
    }
}
