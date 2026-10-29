using System.Text;
using System.Text.RegularExpressions;

namespace NetProxy.Plugins.Redirect
{
    /// <summary>
    /// The URL a request was made for.
    /// </summary>
    internal readonly record struct RequestedUrl(string Scheme, string Host, int Port, string Path, string Query)
    {
        public bool IsDefaultPort => Port == (Scheme == "https" ? 443 : 80);

        /// <summary>The host with its port when that is not the usual one for the scheme.</summary>
        public string HostPort => IsDefaultPort ? Host : $"{Host}:{Port}";

        public override string ToString() => $"{Scheme}://{HostPort}{Path}{Query}";
    }

    /// <summary>
    /// One rule: "match -> target [status] [keepquery]". The match is a URL with * for anything (http://*.example.com/old/*), the target is
    /// where to send the client, and it can use what the match caught: {1} to {9} are the parts that * stood for (in the host and then in
    /// the path), and {scheme}, {host}, {port}, {hostport}, {path}, {query} (with its ?) and {url} (path and query) are the URL of the request.
    ///
    ///   http://ntdls.com/page.html -> https://ntdls.com/page.html
    ///   http://example.com/page.html -> http://example.com
    ///   http://*/* -> https://{host}{url}
    ///   http://*.example.com/old/* -> https://www.example.com/new/{2} 302
    ///
    /// A match without a scheme is for both, one without a path is for every path. The query string is not part of the match, and is not
    /// sent on unless the target says {query} (or {url}), or the rule says keepquery.
    /// </summary>
    internal sealed class RedirectRule
    {
        private static readonly int[] Statuses = [301, 302, 303, 307, 308];

        private Regex _host = null!;
        private Regex _path = null!;
        private string? _scheme;
        private int? _port;
        private string _target = string.Empty;
        private bool _keepQuery;

        public string Text { get; private set; } = string.Empty;

        /// <summary>0 for the default status of the plug-in.</summary>
        public int Status { get; private set; }

        public static RedirectRule? Parse(string text, out string? error)
        {
            error = null;
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 3 || tokens[1] != "->")
            {
                error = "A rule is 'match -> target', for example 'http://example.com/old -> https://example.com/new'.";
                return null;
            }

            var match = Regex.Match(tokens[0], @"^(?:(?<scheme>https?|\*)://)?(?<host>[^/:]+)(?::(?<port>\d{1,5}|\*))?(?<path>/.*)?$", RegexOptions.IgnoreCase);
            if (match.Success == false)
            {
                error = $"'{tokens[0]}' is not something to match: use [http://|https://]host[:port][/path], with * for anything.";
                return null;
            }

            var target = tokens[2];
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == false && target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == false
                && target.StartsWith("{scheme}", StringComparison.Ordinal) == false && target.StartsWith('/') == false)
            {
                error = $"'{target}' is not somewhere to send the client: start it with http://, https://, {{scheme}} or /.";
                return null;
            }

            var rule = new RedirectRule { Text = text, _target = target };

            var scheme = match.Groups["scheme"].Value.ToLowerInvariant();
            rule._scheme = scheme is "" or "*" ? null : scheme;

            if (match.Groups["port"].Success && match.Groups["port"].Value != "*")
            {
                rule._port = int.Parse(match.Groups["port"].Value);
            }

            rule._host = Wildcard(match.Groups["host"].Value);
            rule._path = match.Groups["path"].Success ? Wildcard(match.Groups["path"].Value) : Wildcard("*");

            foreach (var option in tokens.Skip(3))
            {
                if (int.TryParse(option, out var status) && Statuses.Contains(status))
                {
                    rule.Status = status;
                }
                else if (option.Equals("keepquery", StringComparison.OrdinalIgnoreCase))
                {
                    rule._keepQuery = true;
                }
                else
                {
                    error = $"'{option}' is not an option of a rule: use a status ({string.Join(", ", Statuses)}) or keepquery.";
                    return null;
                }
            }

            return rule;
        }

        private static Regex Wildcard(string pattern)
            => new("^" + string.Join("(.*)", pattern.Split('*').Select(Regex.Escape)) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Where the request is to be sent, or null when the rule is not for it (or would send it to where it already is).
        /// </summary>
        public string? Apply(RequestedUrl url)
        {
            if (_scheme != null && _scheme != url.Scheme)
            {
                return null;
            }

            if (_port != null && _port != url.Port)
            {
                return null;
            }

            var host = _host.Match(url.Host);
            var path = _path.Match(url.Path);
            if (host.Success == false || path.Success == false)
            {
                return null;
            }

            var captures = host.Groups.Cast<Group>().Skip(1).Concat(path.Groups.Cast<Group>().Skip(1)).Select(o => o.Value).ToList();
            var location = Expand(url, captures);

            return IsSameUrl(location, url) ? null : location;
        }

        /// <summary>
        /// True when the location is the URL that was asked for: sending the client there would only make it ask again, for ever.
        /// "http://example.com" and "http://example.com/" are the same URL.
        /// </summary>
        private static bool IsSameUrl(string location, RequestedUrl url)
        {
            if (location.StartsWith('/'))
            {
                return string.Equals(location, url.Path + url.Query, StringComparison.Ordinal);
            }

            if (Uri.TryCreate(location, UriKind.Absolute, out var uri) == false)
            {
                return false;
            }

            return uri.Scheme == url.Scheme
                && string.Equals(uri.IdnHost, url.Host, StringComparison.OrdinalIgnoreCase)
                && uri.Port == url.Port
                && string.Equals(uri.PathAndQuery, url.Path + url.Query, StringComparison.Ordinal);
        }

        private string Expand(RequestedUrl url, List<string> captures)
        {
            var usesQuery = _target.Contains("{query}") || _target.Contains("{url}") || _target.Contains('?');
            var expanded = new StringBuilder();

            for (var i = 0; i < _target.Length; i++)
            {
                var close = _target[i] == '{' ? _target.IndexOf('}', i) : -1;
                var value = close > 0 ? Value(_target[(i + 1)..close], url, captures) : null;

                if (value == null)
                {
                    expanded.Append(_target[i]);
                    continue;
                }

                expanded.Append(value);
                i = close;
            }

            if (_keepQuery && usesQuery == false)
            {
                expanded.Append(url.Query);
            }

            return expanded.ToString();
        }

        private static string? Value(string name, RequestedUrl url, List<string> captures)
        {
            switch (name)
            {
                case "scheme": return url.Scheme;
                case "host": return url.Host;
                case "port": return url.Port.ToString();
                case "hostport": return url.HostPort;
                case "path": return url.Path;
                case "query": return url.Query;
                case "url": return url.Path + url.Query;
            }

            //A part that the match did not catch is nothing, so that a rule can be written for more than one shape of URL.
            return name.Length == 1 && char.IsAsciiDigit(name[0]) && name[0] != '0' ? (name[0] - '1' < captures.Count ? captures[name[0] - '1'] : string.Empty) : null;
        }
    }
}
