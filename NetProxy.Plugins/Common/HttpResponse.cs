using System.Net;
using System.Text;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// Makes the bytes of an HTTP/1.1 response, for a plug-in that answers the client itself.
    /// </summary>
    internal static class HttpResponse
    {
        public static byte[] Build(int status, string contentType, byte[] body, bool close, IEnumerable<(string Name, string Value)>? headers = null)
        {
            var head = new StringBuilder();

            head.Append("HTTP/1.1 ").Append(status).Append(' ').Append(Reason(status)).Append("\r\n");

            if (body.Length > 0 || status is not (204 or 304))
            {
                head.Append("Content-Type: ").Append(contentType).Append("\r\n");
                head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            }

            foreach (var (name, value) in headers ?? [])
            {
                //A header value must not be able to end the head.
                head.Append(name).Append(": ").Append(value.Replace('\r', ' ').Replace('\n', ' ')).Append("\r\n");
            }

            head.Append("Connection: ").Append(close ? "close" : "keep-alive").Append("\r\n\r\n");

            return [.. Encoding.ASCII.GetBytes(head.ToString()), .. body];
        }

        public static byte[] Text(int status, string text, bool close, IEnumerable<(string Name, string Value)>? headers = null)
            => Build(status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), close, headers);

        public static string Reason(int status)
            => status switch
            {
                200 => "OK",
                301 => "Moved Permanently",
                302 => "Found",
                307 => "Temporary Redirect",
                308 => "Permanent Redirect",
                401 => "Unauthorized",
                403 => "Forbidden",
                500 => "Internal Server Error",
                502 => "Bad Gateway",
                503 => "Service Unavailable",
                504 => "Gateway Timeout",
                _ => Enum.IsDefined(typeof(HttpStatusCode), status)
                    ? string.Concat(((HttpStatusCode)status).ToString().Select((c, i) => i > 0 && char.IsUpper(c) && char.IsLower(((HttpStatusCode)status).ToString()[i - 1]) ? " " + c : c.ToString()))
                    : "Unknown"
            };
    }
}
