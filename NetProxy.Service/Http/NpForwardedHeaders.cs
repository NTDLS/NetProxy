using NetProxy.Library;
using NetProxy.Service.Http.Http2;
using System.Text.RegularExpressions;

namespace NetProxy.Service.Http
{
    /// <summary>
    /// Tells the endpoint who the client was, and how it connected, which it cannot tell for itself when the connection comes from the
    /// proxy: X-Forwarded-For (the address of the client) and X-Forwarded-Proto (http or https).
    ///
    /// Replace: whatever the client sent for these is dropped, because anybody can send them. This is for a proxy that clients
    ///   connect to directly.
    /// Append: the address of the client is added to the end of the X-Forwarded-For that is there, and X-Forwarded-Proto is kept if it
    ///   is there. This is for a proxy that sits behind another proxy which sets them.
    /// </summary>
    internal sealed class NpForwardedHeaders(ForwardedHeadersMode mode, string clientAddress, string scheme)
    {
        public const string ForwardedFor = "X-Forwarded-For";
        public const string ForwardedProto = "X-Forwarded-Proto";

        private static readonly Regex _forwardedForFinder =
            new(@"^X-Forwarded-For[ \t]*:[ \t]*([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Adds the headers to the header block of an HTTP/1.x request.
        /// </summary>
        public string ApplyToHttp1Request(string header, string delimiter)
        {
            if (mode == ForwardedHeadersMode.Replace)
            {
                header = HttpUtility.UpsertHttpHostHeaderValue(header, ForwardedFor, clientAddress, delimiter);
                return HttpUtility.UpsertHttpHostHeaderValue(header, ForwardedProto, scheme, delimiter);
            }

            var match = _forwardedForFinder.Match(header);
            header = match.Success && match.Groups[1].Value.Trim().Length > 0
                ? _forwardedForFinder.Replace(header, _ => $"{ForwardedFor}: {match.Groups[1].Value.Trim()}, {clientAddress}", 1)
                : HttpUtility.UpsertHttpHostHeaderValue(header, ForwardedFor, clientAddress, delimiter);

            return HttpUtility.InsertHttpHostHeaderValue(header, ForwardedProto, scheme, delimiter);
        }

        /// <summary>
        /// Adds the headers to the fields of an HTTP/2 request.
        /// </summary>
        public void ApplyToHttp2Request(List<HeaderField> fields)
        {
            var forwardedFor = ForwardedFor.ToLowerInvariant();
            var forwardedProto = ForwardedProto.ToLowerInvariant();

            if (mode == ForwardedHeadersMode.Replace)
            {
                fields.RemoveAll(f => Is(f, forwardedFor) || Is(f, forwardedProto));
                fields.Add(new HeaderField(forwardedFor, clientAddress));
                fields.Add(new HeaderField(forwardedProto, scheme));
                return;
            }

            var existing = fields.Where(f => Is(f, forwardedFor)).Select(f => f.ValueText.Trim()).Where(o => o.Length > 0).ToList();
            fields.RemoveAll(f => Is(f, forwardedFor));
            fields.Add(new HeaderField(forwardedFor, string.Join(", ", existing.Append(clientAddress))));

            if (fields.Any(f => Is(f, forwardedProto)) == false)
            {
                fields.Add(new HeaderField(forwardedProto, scheme));
            }
        }

        private static bool Is(HeaderField field, string lowerCaseName)
            => field.NameText.Equals(lowerCaseName, StringComparison.OrdinalIgnoreCase);
    }
}
