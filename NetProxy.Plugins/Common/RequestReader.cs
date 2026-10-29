using System.Text;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// A request that went by: what a plug-in needs to know about it.
    /// </summary>
    /// <param name="BodyPending">True when the body of the request is still to come (or has no known end), so a connection that is answered cannot be reused.</param>
    /// <param name="Headers">All the headers of the request. Names are not case sensitive; a header that is sent twice is joined with a comma.</param>
    internal sealed record SeenRequest(string Method, string Target, string? Host, string? ForwardedFor, string? ConnectingIp, bool BodyPending,
        IReadOnlyDictionary<string, string>? Headers = null)
    {
        public string? Header(string name) => Headers != null && Headers.TryGetValue(name, out var value) ? value : null;

        /// <summary>The path of the target, without the query string.</summary>
        public string Path => Split().Path;

        /// <summary>The query string with its "?", or an empty string.</summary>
        public string Query => Split().Query;

        private (string Path, string Query) Split()
        {
            var target = Target;

            //The absolute form: "GET http://example.com/path HTTP/1.1".
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var slash = target.IndexOfAny(['/', '?'], target.IndexOf("//", StringComparison.Ordinal) + 2);
                target = slash < 0 ? "/" : target[slash..];
            }

            var hash = target.IndexOf('#');
            if (hash >= 0)
            {
                target = target[..hash];
            }

            var question = target.IndexOf('?');
            var path = question < 0 ? target : target[..question];
            var query = question < 0 || question == target.Length - 1 ? string.Empty : target[question..];

            return (path.Length == 0 ? "/" : path, query);
        }
    }

    /// <summary>
    /// Finds the requests in what a client sends over one HTTP/1.x connection. It is fed the reads as they come, in pieces of any size: a
    /// head can arrive in parts, and several requests can arrive in one read. Bodies are skipped (by their Content-Length, or chunk by chunk)
    /// so that what is inside one is not taken for a request. It only looks, and it never throws.
    ///
    /// What it does not follow makes it stop for the rest of the connection, and it then costs nothing: a connection that was upgraded
    /// (a WebSocket, a tunnel) is <see cref="IsStopped"/>, and one that is not HTTP/1.x, or has a head that is too big, is
    /// <see cref="IsFaulted"/> as well.
    ///
    /// With <paramref name="keepBuffer"/> it also lets a plug-in hold back what it has not decided about: <see cref="Buffer"/> is
    /// everything that was fed (what was held back before, and the new data) and <see cref="HeldFrom"/> is where the head that has not
    /// arrived whole starts. A plug-in that forwards <c>Buffer[..HeldFrom]</c> never lets a half of a request through.
    /// </summary>
    internal sealed class RequestReader(bool keepBuffer = false)
    {
        private const int MaxHeadBytes = 16 * 1024;
        private const int MaxLineBytes = 1024;

        private enum Mode { Head, Body, ChunkSize, ChunkData, ChunkDataEnd, Trailers }

        private readonly List<byte> _line = [];
        private byte[] _pending = [];
        private Mode _mode = Mode.Head;
        private long _remaining;

        /// <summary>True when the reader does not follow the connection any more.</summary>
        public bool IsStopped { get; private set; }

        /// <summary>True when it stopped because it did not understand what it saw, and not because the connection was upgraded.</summary>
        public bool IsFaulted { get; private set; }

        /// <summary>With keepBuffer: what was held back before and what was just fed.</summary>
        public byte[] Buffer { get; private set; } = [];

        /// <summary>With keepBuffer: the start of the part of <see cref="Buffer"/> that is a head that has not arrived whole. The buffer's length if there is none.</summary>
        public int HeldFrom { get; private set; }

        public List<SeenRequest> Feed(ReadOnlySpan<byte> data)
        {
            var found = new List<SeenRequest>();

            try
            {
                byte[]? combined = null;
                var span = data;

                if (_pending.Length > 0)
                {
                    combined = new byte[_pending.Length + data.Length];
                    _pending.CopyTo(combined, 0);
                    data.CopyTo(combined.AsSpan(_pending.Length));
                    span = combined;
                    _pending = [];
                }

                if (keepBuffer)
                {
                    Buffer = combined ?? data.ToArray();
                }

                HeldFrom = span.Length;

                if (IsStopped)
                {
                    return found;
                }

                var offset = 0;

                while (offset < span.Length && IsStopped == false)
                {
                    switch (_mode)
                    {
                        case Mode.Head:
                            offset = ReadHead(span, offset, found);
                            break;

                        case Mode.Body:
                        case Mode.ChunkData:
                        case Mode.ChunkDataEnd:
                            {
                                var take = (int)Math.Min(_remaining, span.Length - offset);
                                offset += take;
                                _remaining -= take;

                                if (_remaining == 0)
                                {
                                    switch (_mode)
                                    {
                                        case Mode.Body: _mode = Mode.Head; break;
                                        case Mode.ChunkData: _mode = Mode.ChunkDataEnd; _remaining = 2; break; //The CRLF that ends the chunk.
                                        default: _mode = Mode.ChunkSize; break;
                                    }
                                }
                                break;
                            }

                        case Mode.ChunkSize:
                        case Mode.Trailers:
                            offset = ReadChunkLine(span, offset);
                            break;
                    }
                }
            }
            catch
            {
                Fault();
            }

            return found;
        }

        private void Fault()
        {
            IsStopped = true;
            IsFaulted = true;
            _pending = [];
        }

        private int ReadHead(ReadOnlySpan<byte> span, int offset, List<SeenRequest> found)
        {
            var rest = span[offset..];

            if (LooksLikeRequestStart(rest) == false)
            {
                Fault();
                return span.Length;
            }

            var end = rest.IndexOf("\r\n\r\n"u8);
            if (end < 0)
            {
                if (rest.Length > MaxHeadBytes)
                {
                    Fault();
                }
                else
                {
                    _pending = rest.ToArray();
                    HeldFrom = offset;
                }

                return span.Length;
            }

            var request = ParseHead(Encoding.Latin1.GetString(rest[..end]), out var contentLength, out var chunked, out var upgrades);
            if (request == null)
            {
                Fault();
                return span.Length;
            }

            var headLength = end + 4;

            if (upgrades)
            {
                //A tunnel or an upgraded connection is not HTTP any more after this head.
                found.Add(request);
                IsStopped = true;
                return span.Length;
            }

            if (chunked)
            {
                found.Add(request with { BodyPending = true });
                _mode = Mode.ChunkSize;
                _line.Clear();
                return offset + headLength;
            }

            var inThisRead = rest.Length - headLength;
            found.Add(request with { BodyPending = contentLength > inThisRead });

            if (contentLength > 0)
            {
                _mode = Mode.Body;
                _remaining = contentLength;
            }

            return offset + headLength;
        }

        /// <summary>
        /// Reads a line of the chunk framing (a chunk size, or a trailer) and moves on when it is whole.
        /// </summary>
        private int ReadChunkLine(ReadOnlySpan<byte> span, int offset)
        {
            var rest = span[offset..];
            var index = rest.IndexOf((byte)'\n');
            var take = index < 0 ? rest.Length : index + 1;

            if (_line.Count + take > MaxLineBytes)
            {
                Fault();
                return span.Length;
            }

            foreach (var b in rest[..take])
            {
                _line.Add(b);
            }

            if (index < 0)
            {
                return offset + take;
            }

            var text = Encoding.ASCII.GetString([.. _line]).Trim();
            _line.Clear();

            if (_mode == Mode.Trailers)
            {
                //An empty line ends the message, anything else is a trailer to skip.
                if (text.Length == 0)
                {
                    _mode = Mode.Head;
                }

                return offset + take;
            }

            var semicolon = text.IndexOf(';');
            if (semicolon >= 0)
            {
                text = text[..semicolon];
            }

            if (long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var size) == false || size < 0)
            {
                Fault();
                return span.Length;
            }

            if (size == 0)
            {
                _mode = Mode.Trailers;
            }
            else
            {
                _mode = Mode.ChunkData;
                _remaining = size;
            }

            return offset + take;
        }

        /// <summary>
        /// A request starts with a method in capitals, a space, and a target.
        /// </summary>
        private static bool LooksLikeRequestStart(ReadOnlySpan<byte> data)
        {
            var space = data.IndexOf((byte)' ');
            if (space < 3 || space > 12 || space + 1 >= data.Length)
            {
                return false;
            }

            for (var i = 0; i < space; i++)
            {
                if (data[i] is < (byte)'A' or > (byte)'Z')
                {
                    return false;
                }
            }

            return data[space + 1] is (byte)'/' or (byte)'*' or (byte)'h' or (byte)'H';
        }

        private static SeenRequest? ParseHead(string head, out long contentLength, out bool chunked, out bool upgrades)
        {
            contentLength = 0;
            chunked = false;
            upgrades = false;

            var lines = head.Split("\r\n");
            var start = lines[0].Split(' ', 3);
            if (start.Length < 3 || start[2].StartsWith("HTTP/1.", StringComparison.Ordinal) == false)
            {
                return null;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < lines.Length; i++)
            {
                var colon = lines[i].IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var name = lines[i][..colon].Trim();
                var value = lines[i][(colon + 1)..].Trim();
                headers[name] = headers.TryGetValue(name, out var existing) ? existing + (name.Equals("X-Forwarded-For", StringComparison.OrdinalIgnoreCase) ? "," : ", ") + value : value;
            }

            if (headers.TryGetValue("Content-Length", out var length) && long.TryParse(length, out var parsed) && parsed > 0)
            {
                contentLength = parsed;
            }

            chunked = headers.TryGetValue("Transfer-Encoding", out var encoding) && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase);
            upgrades = start[0] == "CONNECT" || headers.ContainsKey("Upgrade");

            return new SeenRequest(start[0], start[1], headers.GetValueOrDefault("Host"), headers.GetValueOrDefault("X-Forwarded-For"),
                headers.GetValueOrDefault("CF-Connecting-IP"), false, headers);
        }
    }
}
