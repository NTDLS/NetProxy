namespace NetProxy.Library.Utilities
{
    /// <summary>
    /// Host name patterns, as used to tell the proxies that share a port apart: an exact name ("www.example.com") or a wildcard for
    /// the names below a domain ("*.example.com", which does not match "example.com" itself). Names are not case sensitive.
    /// </summary>
    public static class NpHostMatcher
    {
        /// <summary>
        /// The form that names are compared in: trimmed, lower case, without a trailing dot or a port. Null when nothing is left.
        /// </summary>
        public static string? Normalize(string? host)
        {
            var value = host?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.StartsWith('['))
            {
                //A bracketed IPv6 address, with or without a port.
                var close = value.IndexOf(']');
                value = close > 0 ? value[..(close + 1)] : value;
            }
            else
            {
                var colon = value.LastIndexOf(':');
                if (colon >= 0 && value.IndexOf(':') == colon)
                {
                    value = value[..colon];
                }
            }

            value = value.TrimEnd('.');
            return value.Length == 0 ? null : value;
        }

        /// <summary>
        /// True if the pattern is a valid exact name or a "*." wildcard. The pattern is expected to be normalized.
        /// </summary>
        public static bool IsValidPattern(string? pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            var name = pattern.StartsWith("*.") ? pattern[2..] : pattern;

            if (name.Length == 0 || name.Length > 253 || name.Contains('*'))
            {
                return false;
            }

            foreach (var label in name.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63 || label.StartsWith('-') || label.EndsWith('-'))
                {
                    return false;
                }

                foreach (var c in label)
                {
                    if (char.IsAsciiLetterOrDigit(c) == false && c != '-' && c != '_')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// True if the (normalized) host is named by the (normalized) pattern.
        /// </summary>
        public static bool Matches(string pattern, string host)
        {
            if (pattern.StartsWith("*."))
            {
                //One or more labels below the domain.
                return host.Length > pattern.Length - 1 && host.EndsWith(pattern.AsSpan(1), StringComparison.Ordinal);
            }

            return string.Equals(pattern, host, StringComparison.Ordinal);
        }

        /// <summary>
        /// How specific a pattern is: an exact name is above any wildcard, and a longer wildcard is above a shorter one.
        /// </summary>
        public static int Specificity(string pattern) => pattern.StartsWith("*.") ? pattern.Length : int.MaxValue;

        /// <summary>
        /// The most specific of the (normalized) patterns that name the host, or null if none does.
        /// </summary>
        public static string? FindBest(IEnumerable<string> patterns, string host)
        {
            string? best = null;

            foreach (var pattern in patterns)
            {
                if (Matches(pattern, host) && (best == null || Specificity(pattern) > Specificity(best)))
                {
                    best = pattern;
                }
            }

            return best;
        }
    }
}
