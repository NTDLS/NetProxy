using System.Text;

namespace NetProxy.TrafficTracker.Plugin.Http
{
    /// <summary>
    /// The head of an HTTP/1.x request or response: the start line and the headers.
    /// </summary>
    internal sealed class HttpHead
    {
        public bool IsRequest { get; init; }

        /// <summary>Request: the method. Response: the version ("HTTP/1.1").</summary>
        public string First { get; init; } = string.Empty;

        /// <summary>Request: the target ("/path?query"). Response: the status code.</summary>
        public string Second { get; init; } = string.Empty;

        /// <summary>Request: the version ("HTTP/1.1"). Response: the reason.</summary>
        public string Third { get; init; } = string.Empty;

        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string Method => IsRequest ? First : string.Empty;
        public string Target => IsRequest ? Second : string.Empty;
        public string Version => IsRequest ? Third : First;

        public int StatusCode => !IsRequest && int.TryParse(Second, out var status) ? status : 0;

        public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

        public long? ContentLength => Headers.TryGetValue("Content-Length", out var value) && long.TryParse(value, out var length) && length >= 0 ? length : null;

        public bool IsChunked => Headers.TryGetValue("Transfer-Encoding", out var value) && value.Contains("chunked", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Follows one direction of an HTTP/1.x connection and says when the head of a message has gone by. It is fed the bytes as they pass,
    /// in pieces of any size, and skips the bodies (by Content-Length, or chunk by chunk) so that it finds the next head of a keep-alive
    /// connection. It only looks: it never changes what passes, and it never throws. Anything it does not understand (TLS that was not
    /// decrypted, a protocol that is not HTTP/1.x, a head that is too big, a connection that was upgraded) makes it stop for good, and it
    /// then costs nothing.
    /// </summary>
    internal sealed class Http1StreamParser
    {
        private const int MaxHeadBytes = 32 * 1024;
        private const int MaxLineBytes = 1024;

        private enum State { Head, Body, ChunkSize, ChunkData, ChunkDataEnd, Trailers, Stopped }

        private readonly bool _isRequest;
        private readonly Func<HttpHead, bool> _onHead;

        private State _state = State.Head;
        private byte[] _head = new byte[1024];
        private int _headLength;
        private int _searchFrom;
        private long _remaining;
        private readonly List<byte> _line = [];

        /// <param name="isRequest">True for the direction from the client, false for the answers.</param>
        /// <param name="onHead">
        /// Called with every head. For a response it returns true when the message has no body whatever its headers say (the response to a
        /// HEAD request); for a request the result is not used.
        /// </param>
        public Http1StreamParser(bool isRequest, Func<HttpHead, bool> onHead)
        {
            _isRequest = isRequest;
            _onHead = onHead;
        }

        /// <summary>True when the parser gave up, or the connection is not something it follows any more.</summary>
        public bool IsStopped => _state == State.Stopped;

        public void Feed(ReadOnlySpan<byte> data)
        {
            try
            {
                while (data.Length > 0 && _state != State.Stopped)
                {
                    switch (_state)
                    {
                        case State.Head:
                            data = ConsumeHead(data);
                            break;

                        case State.Body:
                            {
                                var count = (int)Math.Min(_remaining, data.Length);
                                data = data[count..];
                                _remaining -= count;
                                if (_remaining == 0)
                                {
                                    _state = State.Head;
                                }
                                break;
                            }

                        case State.ChunkSize:
                            data = ConsumeChunkSize(data);
                            break;

                        case State.ChunkData:
                            {
                                var count = (int)Math.Min(_remaining, data.Length);
                                data = data[count..];
                                _remaining -= count;
                                if (_remaining == 0)
                                {
                                    _remaining = 2; //The CRLF that ends the chunk.
                                    _state = State.ChunkDataEnd;
                                }
                                break;
                            }

                        case State.ChunkDataEnd:
                            {
                                var count = (int)Math.Min(_remaining, data.Length);
                                data = data[count..];
                                _remaining -= count;
                                if (_remaining == 0)
                                {
                                    _state = State.ChunkSize;
                                }
                                break;
                            }

                        case State.Trailers:
                            data = ConsumeTrailers(data);
                            break;
                    }
                }
            }
            catch
            {
                //Whatever went wrong, the traffic is not to be affected: this stream is just no longer followed.
                Stop();
            }
        }

        private void Stop()
        {
            _state = State.Stopped;
            _head = [];
            _headLength = 0;
            _line.Clear();
        }

        private ReadOnlySpan<byte> ConsumeHead(ReadOnlySpan<byte> data)
        {
            //The first bytes say whether this is HTTP at all, so that other traffic (TLS that was not decrypted, for one) is dropped at once.
            if (_headLength == 0 && LooksLikeHttp(data) == false)
            {
                Stop();
                return [];
            }

            if (_headLength + data.Length > _head.Length)
            {
                Array.Resize(ref _head, Math.Max(_head.Length * 2, _headLength + data.Length));
            }

            data.CopyTo(_head.AsSpan(_headLength));
            var previousLength = _headLength;
            _headLength += data.Length;

            var searchFrom = Math.Max(0, Math.Max(previousLength - 3, _searchFrom));
            var end = IndexOfHeadEnd(_head.AsSpan(0, _headLength), searchFrom);

            if (end < 0)
            {
                _searchFrom = Math.Max(0, _headLength - 3);

                if (_headLength > MaxHeadBytes)
                {
                    Stop();
                }

                return [];
            }

            var headEnd = end + 4;
            var leftover = _head.AsSpan(headEnd, _headLength - headEnd).ToArray();
            var head = ParseHead(_head.AsSpan(0, end));

            _headLength = 0;
            _searchFrom = 0;
            if (_head.Length > MaxHeadBytes)
            {
                _head = new byte[1024];
            }

            if (head == null)
            {
                Stop();
                return [];
            }

            var noBody = _onHead(head);
            DecideBody(head, noBody);

            return leftover;
        }

        private bool LooksLikeHttp(ReadOnlySpan<byte> data)
        {
            if (_isRequest)
            {
                //A method: upper case letters (the ones that matter are GET, POST, PUT, ... but any token is one).
                return data[0] is >= (byte)'A' and <= (byte)'Z';
            }

            //"HTTP/", as far as the bytes go.
            var expected = "HTTP/"u8;
            var count = Math.Min(expected.Length, data.Length);
            return data[..count].SequenceEqual(expected[..count]);
        }

        private static int IndexOfHeadEnd(ReadOnlySpan<byte> buffer, int from)
        {
            var found = buffer[from..].IndexOf("\r\n\r\n"u8);
            return found < 0 ? -1 : from + found;
        }

        private HttpHead? ParseHead(ReadOnlySpan<byte> text)
        {
            var lines = Encoding.Latin1.GetString(text).Split("\r\n");
            var parts = lines[0].Split(' ', 3);

            if (parts.Length < 3 && _isRequest || parts.Length < 2)
            {
                return null;
            }

            if (_isRequest)
            {
                if (parts[0].Length == 0 || parts[1].Length == 0 || parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal) == false)
                {
                    return null;
                }
            }
            else if (parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) == false || int.TryParse(parts[1], out _) == false)
            {
                return null;
            }

            var head = new HttpHead
            {
                IsRequest = _isRequest,
                First = parts[0],
                Second = parts[1],
                Third = parts.Length > 2 ? parts[2] : string.Empty
            };

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colon = line.IndexOf(':');

                //A header that is folded onto the next line (obsolete) is not understood, and neither is a line that is not a header.
                if (colon <= 0 || line[0] is ' ' or '\t')
                {
                    return null;
                }

                var name = line[..colon].Trim();
                if (head.Headers.ContainsKey(name) == false)
                {
                    head.Headers[name] = line[(colon + 1)..].Trim();
                }
            }

            return head;
        }

        private void DecideBody(HttpHead head, bool noBody)
        {
            if (_isRequest)
            {
                //A tunnel or an upgraded connection is not HTTP any more after this head.
                if (head.Method == "CONNECT" || head.Headers.ContainsKey("Upgrade"))
                {
                    Stop();
                    return;
                }
            }
            else
            {
                var status = head.StatusCode;

                if (status == 101)
                {
                    Stop();
                    return;
                }

                if (status is >= 100 and < 200 || status is 204 or 304 || noBody)
                {
                    _state = State.Head;
                    return;
                }
            }

            if (head.IsChunked)
            {
                _state = State.ChunkSize;
                _line.Clear();
                return;
            }

            var length = head.ContentLength;
            if (length is > 0)
            {
                _remaining = length.Value;
                _state = State.Body;
                return;
            }

            if (length == 0 || _isRequest)
            {
                _state = State.Head; //No body.
                return;
            }

            //A response without a length ends when the connection does, so there is no next head to find.
            Stop();
        }

        /// <summary>
        /// Reads up to the end of a line (LF) into the line buffer. Returns the rest of the data, and sets <paramref name="complete"/> when the
        /// line is whole.
        /// </summary>
        private ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> data, out bool complete)
        {
            var index = data.IndexOf((byte)'\n');
            var take = index < 0 ? data.Length : index + 1;

            if (_line.Count + take > MaxLineBytes)
            {
                Stop();
                complete = false;
                return [];
            }

            foreach (var b in data[..take])
            {
                _line.Add(b);
            }

            complete = index >= 0;
            return data[take..];
        }

        private ReadOnlySpan<byte> ConsumeChunkSize(ReadOnlySpan<byte> data)
        {
            data = ReadLine(data, out var complete);
            if (complete == false)
            {
                return data;
            }

            var text = Encoding.ASCII.GetString([.. _line]).Trim();
            _line.Clear();

            var semicolon = text.IndexOf(';');
            if (semicolon >= 0)
            {
                text = text[..semicolon];
            }

            if (long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var size) == false || size < 0)
            {
                Stop();
                return [];
            }

            if (size == 0)
            {
                _state = State.Trailers;
            }
            else
            {
                _remaining = size;
                _state = State.ChunkData;
            }

            return data;
        }

        private ReadOnlySpan<byte> ConsumeTrailers(ReadOnlySpan<byte> data)
        {
            data = ReadLine(data, out var complete);
            if (complete == false)
            {
                return data;
            }

            //An empty line ends the message, anything else is a trailer header to skip.
            var empty = _line.Count == 2 && _line[0] == '\r' || _line.Count == 1;
            _line.Clear();

            if (empty)
            {
                _state = State.Head;
            }

            return data;
        }
    }
}
