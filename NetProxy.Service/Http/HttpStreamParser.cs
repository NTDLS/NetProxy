using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NetProxy.Service.Http
{
    /// <summary>
    /// Follows the HTTP/1.x message framing of one side of an endpoint edge connection so that header rules
    ///     can be applied to every message on a keep-alive connection, not just the first. Message bodies are
    ///     tracked using Content-Length or chunked transfer-encoding so that body bytes are never mistaken for
    ///     a header (and headers which do not land at the start of a receive buffer are still found).
    ///
    /// When the stream stops looking like HTTP (an upgrade such as WebSockets, CONNECT, a response that is
    ///     delimited by closing the connection, or simply non-HTTP data) the parser switches to passthrough
    ///     for the remainder of the connection.
    ///
    /// Known limitation: a response to a HEAD request carries a Content-Length but no body. The parser only
    ///     sees one direction of the conversation so it cannot know the request was a HEAD; it will treat
    ///     the start of the next response as body and then pass the rest of that connection through.
    /// </summary>
    internal class HttpStreamParser : IHttpStreamProcessor
    {
        /// <summary>
        /// Headers larger than this are not modified; the connection is passed through instead.
        /// </summary>
        private const int MaxHeaderBytes = 64 * 1024;

        /// <summary>
        /// Chunk-size and trailer lines larger than this are considered malformed.
        /// </summary>
        private const int MaxLineBytes = 8 * 1024;

        private static readonly string[] HttpStartTokens =
            HttpUtility.HttpVerbStrings.Select(o => $"{o.ToUpperInvariant()} ").Append("HTTP/").ToArray();

        private static readonly Regex ContentLengthFinder =
            new(@"^Content-Length[ \t]*:[ \t]*(\d+)[ \t]*\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex TransferEncodingFinder =
            new(@"^Transfer-Encoding[ \t]*:([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private enum ParserState
        {
            Header,
            Body,
            ChunkSize,
            ChunkData,
            ChunkDataEnd,
            Trailer,
            Passthrough
        }

        private readonly NpProxyConfiguration _configuration;
        private readonly ConnectionDirection _direction;
        private readonly MemoryStream _pending = new();
        private ParserState _state = ParserState.Header;
        private long _remaining;

        /// <param name="configuration">The proxy, for its header rules.</param>
        /// <param name="direction">Inbound when the bytes come from the client (requests), Outbound when they come from an endpoint (responses).</param>
        private readonly NpForwardedHeaders? _forwarded;

        public HttpStreamParser(NpProxyConfiguration configuration, ConnectionDirection direction, NpForwardedHeaders? forwarded = null)
        {
            _configuration = configuration;
            _direction = direction;
            _forwarded = forwarded;
        }

        /// <summary>
        /// Processes bytes received from the edge connection and returns the bytes that should be sent to the peer.
        /// The returned length may be zero when the bytes are being held while waiting on the rest of a header.
        /// </summary>
        public (byte[] Bytes, int Length) Process(byte[] bytes, int length)
        {
            //Fast paths: nothing to inspect, forward the receive buffer as-is.
            if (_state == ParserState.Passthrough)
            {
                return (bytes, length);
            }
            if (_state == ParserState.Body && _remaining >= length)
            {
                _remaining -= length;
                if (_remaining == 0)
                {
                    _state = ParserState.Header;
                }
                return (bytes, length);
            }

            var output = new MemoryStream(length + 256);
            int position = 0;

            while (position < length)
            {
                switch (_state)
                {
                    case ParserState.Passthrough:
                        output.Write(bytes, position, length - position);
                        position = length;
                        break;

                    case ParserState.Body:
                    case ParserState.ChunkData:
                        {
                            int take = (int)Math.Min(_remaining, length - position);
                            output.Write(bytes, position, take);
                            position += take;
                            _remaining -= take;

                            if (_remaining == 0)
                            {
                                _state = _state == ParserState.Body ? ParserState.Header : ParserState.ChunkDataEnd;
                            }
                        }
                        break;

                    case ParserState.Header:
                        position = ConsumeHeader(bytes, position, length, output);
                        break;

                    case ParserState.ChunkSize:
                    case ParserState.ChunkDataEnd:
                    case ParserState.Trailer:
                        position = ConsumeLine(bytes, position, length, output);
                        break;
                }
            }

            return (output.GetBuffer(), (int)output.Length);
        }

        private int ConsumeHeader(byte[] bytes, int position, int length, MemoryStream output)
        {
            while (position < length)
            {
                _pending.WriteByte(bytes[position++]);

                var pending = _pending.GetBuffer();
                int pendingLength = (int)_pending.Length;

                //Until we have enough bytes to recognize the start of a request or response line, make sure it still could be one.
                if (pendingLength <= 8 && IsPlausibleHttpStart(pending, pendingLength) == false)
                {
                    BeginPassthrough(output, null);
                    return position;
                }

                if (pendingLength > MaxHeaderBytes)
                {
                    BeginPassthrough(output, $"HTTP header exceeded {MaxHeaderBytes:n0} bytes on proxy '{_configuration.Name}'.");
                    return position;
                }

                bool isEndOfHeader =
                    (pendingLength >= 2 && pending[pendingLength - 1] == '\n' && pending[pendingLength - 2] == '\n')
                    || (pendingLength >= 4 && pending[pendingLength - 1] == '\n' && pending[pendingLength - 2] == '\r'
                        && pending[pendingLength - 3] == '\n' && pending[pendingLength - 4] == '\r');

                if (isEndOfHeader)
                {
                    CompleteHeader(output);
                    return position;
                }
            }

            return position;
        }

        private void CompleteHeader(MemoryStream output)
        {
            //Latin1 maps bytes to chars 1:1 so that nothing in the header is altered unless a rule alters it.
            var header = Encoding.Latin1.GetString(_pending.GetBuffer(), 0, (int)_pending.Length);
            _pending.SetLength(0);

            var headerType = HttpUtility.IsHttpHeader(header, out var verb);
            if (headerType == HttpHeaderType.None || verb == null || HttpUtility.GetHttpHeaderEnd(header, out var delimiter) < 0)
            {
                //Started out looking like HTTP, but isn't.
                output.Write(Encoding.Latin1.GetBytes(header));
                _state = ParserState.Passthrough;
                return;
            }

            if (_direction == ConnectionDirection.Inbound && headerType == HttpHeaderType.Response)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, "A HTTP response header was detected on the listener.");
            }
            else if (_direction == ConnectionDirection.Outbound && headerType == HttpHeaderType.Request)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, "A HTTP request header was detected on an endpoint.");
            }

            //Before the rules, so that a rule can still change what these say.
            if (_direction == ConnectionDirection.Inbound && headerType == HttpHeaderType.Request && _forwarded != null)
            {
                header = _forwarded.ApplyToHttp1Request(header, delimiter);
            }

            header = HttpUtility.ApplyHttpHeaderRules(_configuration, header, headerType, verb, delimiter);
            output.Write(Encoding.Latin1.GetBytes(header));

            //Determine how the message body (if any) is delimited, using the header as it will be seen by the receiver.
            _state = GetBodyState(header, headerType, verb, out _remaining);
        }

        private static ParserState GetBodyState(string header, HttpHeaderType headerType, string verb, out long contentLength)
        {
            contentLength = 0;

            var transferEncoding = TransferEncodingFinder.Match(header);
            bool isChunked = transferEncoding.Success
                && transferEncoding.Groups[1].Value.Contains("chunked", StringComparison.OrdinalIgnoreCase);

            var contentLengthMatch = ContentLengthFinder.Match(header);
            bool hasContentLength = contentLengthMatch.Success
                && long.TryParse(contentLengthMatch.Groups[1].Value, out contentLength);

            if (headerType == HttpHeaderType.Request)
            {
                if (verb.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    return ParserState.Passthrough; //Everything after CONNECT is an opaque tunnel.
                }
                if (isChunked)
                {
                    return ParserState.ChunkSize;
                }
                return hasContentLength && contentLength > 0 ? ParserState.Body : ParserState.Header;
            }

            int statusCode = GetResponseStatusCode(header);

            if (statusCode == 101)
            {
                return ParserState.Passthrough; //Switching protocols (e.g. WebSockets).
            }
            if ((statusCode >= 100 && statusCode < 200) || statusCode == 204 || statusCode == 304)
            {
                return ParserState.Header; //These responses never have a body.
            }
            if (isChunked)
            {
                return ParserState.ChunkSize;
            }
            if (hasContentLength)
            {
                return contentLength > 0 ? ParserState.Body : ParserState.Header;
            }

            return ParserState.Passthrough; //The body is delimited by the server closing the connection.
        }

        private static int GetResponseStatusCode(string header)
        {
            //e.g. "HTTP/1.1 200 OK"
            var firstLine = header.Substring(0, Math.Max(0, header.IndexOf('\n'))).Trim();
            var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && int.TryParse(parts[1], out int statusCode) ? statusCode : 0;
        }

        /// <summary>
        /// Consumes a single line (chunk-size, the CRLF after chunk data, or a trailer line), forwarding it unchanged.
        /// </summary>
        private int ConsumeLine(byte[] bytes, int position, int length, MemoryStream output)
        {
            while (position < length)
            {
                byte b = bytes[position++];
                output.WriteByte(b);
                _pending.WriteByte(b);

                if (_pending.Length > MaxLineBytes)
                {
                    _pending.SetLength(0);
                    BeginPassthrough(null, $"Malformed chunked encoding (line too long) on proxy '{_configuration.Name}'.");
                    return position;
                }

                if (b == '\n')
                {
                    var line = Encoding.Latin1.GetString(_pending.GetBuffer(), 0, (int)_pending.Length).TrimEnd('\r', '\n');
                    _pending.SetLength(0);
                    CompleteLine(line);
                    return position;
                }
            }

            return position;
        }

        private void CompleteLine(string line)
        {
            switch (_state)
            {
                case ParserState.ChunkSize:
                    {
                        var sizeText = line.Split(';', 2)[0].Trim(); //Ignore chunk extensions.
                        if (long.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long chunkSize) == false || chunkSize < 0)
                        {
                            BeginPassthrough(null, $"Malformed chunked encoding (bad chunk size) on proxy '{_configuration.Name}'.");
                        }
                        else if (chunkSize == 0)
                        {
                            _state = ParserState.Trailer; //Last chunk, optional trailers follow.
                        }
                        else
                        {
                            _remaining = chunkSize;
                            _state = ParserState.ChunkData;
                        }
                    }
                    break;

                case ParserState.ChunkDataEnd:
                    _state = ParserState.ChunkSize;
                    break;

                case ParserState.Trailer:
                    if (line.Length == 0)
                    {
                        _state = ParserState.Header; //End of the message.
                    }
                    break;
            }
        }

        /// <summary>
        /// Stops parsing for the remainder of the connection, forwarding any held header bytes as-is.
        /// </summary>
        private void BeginPassthrough(MemoryStream? output, string? warning)
        {
            if (output != null && _pending.Length > 0)
            {
                output.Write(_pending.GetBuffer(), 0, (int)_pending.Length);
            }
            _pending.SetLength(0);
            _state = ParserState.Passthrough;

            if (warning != null)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"{warning} HTTP header rules will not be applied to the rest of the connection.");
            }
        }

        private static bool IsPlausibleHttpStart(byte[] pending, int pendingLength)
        {
            foreach (var token in HttpStartTokens)
            {
                int compareLength = Math.Min(pendingLength, token.Length);
                bool isMatch = true;

                for (int i = 0; i < compareLength; i++)
                {
                    if (char.ToUpperInvariant((char)pending[i]) != token[i])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
