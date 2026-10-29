using NetProxy.Plugins.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NetProxy.Plugins.AccessLog
{
    internal enum AccessLogFormat { Common, Combined, Json }

    /// <summary>
    /// Writes a request as a line of a web server log: the common log format, the combined log format (with the referrer and the user
    /// agent, which most log analysers read), or a line of JSON.
    /// </summary>
    internal static class AccessLogFormatter
    {
        public static string Format(HttpExchange e, AccessLogFormat format)
            => format == AccessLogFormat.Json ? Json(e) : Text(e, format == AccessLogFormat.Combined);

        private static string Text(HttpExchange e, bool combined)
        {
            var line = new StringBuilder();

            line.Append(Token(e.Client)).Append(" - - [")
                .Append(e.StartedUtc.ToString("dd/MMM/yyyy:HH:mm:ss", CultureInfo.InvariantCulture)).Append(" +0000] \"")
                .Append(Quoted($"{e.Method} {e.Path}{e.Query} HTTP/{e.HttpVersion}")).Append("\" ")
                .Append(e.Status > 0 ? e.Status.ToString(CultureInfo.InvariantCulture) : "-").Append(' ')
                .Append(e.ResponseBytes?.ToString(CultureInfo.InvariantCulture) ?? "-");

            if (combined)
            {
                line.Append(" \"").Append(Quoted(e.Referrer ?? "-")).Append("\" \"").Append(Quoted(e.UserAgent ?? "-")).Append('"');
            }

            return line.ToString();
        }

        private static string Json(HttpExchange e)
            => JsonSerializer.Serialize(new
            {
                time = e.StartedUtc.ToString("O", CultureInfo.InvariantCulture),
                client = e.Client,
                host = e.Host,
                method = e.Method,
                path = e.Path,
                query = e.Query.Length > 0 ? e.Query : null,
                version = e.HttpVersion,
                status = e.Status > 0 ? e.Status : (int?)null,
                bytes = e.ResponseBytes,
                requestBytes = e.RequestBytes,
                durationMs = e.Status > 0 ? e.DurationMs : (int?)null,
                contentType = e.ContentType,
                userAgent = e.UserAgent,
                referrer = e.Referrer,
                proxy = e.Proxy,
                tls = e.IsTls
            }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        /// <summary>A value of the line that is not quoted: it must not hold a space, or the columns shift.</summary>
        private static string Token(string value) => value.Length == 0 || value.Any(c => c <= ' ') ? "-" : value;

        /// <summary>
        /// Text inside quotes: a quote or a backslash is escaped and a control character is written as \xNN, so that a client can not end
        /// the quotes, or the line, and forge the entries that follow it.
        /// </summary>
        internal static string Quoted(string value)
        {
            var escaped = new StringBuilder(value.Length);

            foreach (var c in value)
            {
                if (c is '"' or '\\')
                {
                    escaped.Append('\\').Append(c);
                }
                else if (c < ' ' || c == 0x7f)
                {
                    escaped.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
                }
                else
                {
                    escaped.Append(c);
                }
            }

            return escaped.ToString();
        }
    }
}
