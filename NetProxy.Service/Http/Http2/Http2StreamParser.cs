using NetProxy.Library.Payloads.Routing;
using NetProxy.Library;
using System.Buffers.Binary;
using System.Text;

namespace NetProxy.Service.Http.Http2
{
    /// <summary>
    /// Follows one direction of an HTTP/2 connection so that header rules can be applied to the requests (client to server) or to
    /// the responses (server to client) that pass through.
    ///
    /// HTTP/2 is a sequence of frames. Every frame is passed on as it is, except for the ones that carry header blocks (HEADERS,
    /// PUSH_PROMISE and their CONTINUATION frames): those are collected, decoded, changed by the rules, and written again. Header blocks
    /// are compressed with state that is shared by the whole connection (HPACK), so *every* header block in this direction is decoded and
    /// written again, even when no rule applies, and the blocks that are written never use that state (see <see cref="HpackEncoder"/>).
    ///
    /// Stream data is never buffered: DATA and the other frames are passed on as they arrive.
    ///
    /// Anything that is not valid HTTP/2 raises an exception, which ends the connection. The stream cannot be passed through unchanged
    /// from the middle, because the receiver has not seen the compression state the sender assumes.
    /// </summary>
    internal sealed class Http2StreamParser : IHttpStreamProcessor
    {
        private const byte FrameData = 0x0;
        private const byte FrameHeaders = 0x1;
        private const byte FrameRstStream = 0x3;
        private const byte FramePushPromise = 0x5;
        private const byte FrameContinuation = 0x9;

        private const byte FlagEndStream = 0x1;
        private const byte FlagEndHeaders = 0x4;
        private const byte FlagPadded = 0x8;
        private const byte FlagPriority = 0x20;

        private const int FrameHeaderLength = 9;

        /// <summary>
        /// The largest payload that every receiver must accept (SETTINGS_MAX_FRAME_SIZE can only raise it).
        /// </summary>
        private const int DefaultMaxFramePayload = 16384;

        /// <summary>
        /// A header block that is larger than this (after joining its CONTINUATION frames) is refused.
        /// </summary>
        private const int MaxHeaderBlockBytes = 1024 * 1024;

        private static readonly byte[] ClientPreface = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        internal static byte[] ClientPrefaceBytes => ClientPreface;

        private enum ParserState
        {
            Preface,
            FrameHeader,
            Payload
        }

        private readonly NpProxyConfiguration _configuration;
        private readonly bool _fromClient;
        private readonly HpackDecoder _decoder = new();
        private readonly HashSet<uint> _streamsWithHeaders = [];

        private ParserState _state;
        private long _payloadRemaining;

        //Received bytes that have not been consumed yet: _buffer[_start.._end].
        private byte[] _buffer = new byte[4096];
        private int _start;
        private int _end;

        //The header block being collected.
        private bool _collecting;
        private byte _blockFrameType;
        private uint _blockStreamId;
        private bool _blockEndStream;
        private byte[]? _blockPriority;
        private byte[]? _blockPromisedStream;
        private readonly MemoryStream _block = new();

        /// <summary>
        /// When set, the ":scheme" of the requests is changed to this ("http" or "https"). A server can refuse a request whose scheme is
        /// not the one its connection uses, which differs when the proxy terminates TLS for a plain endpoint, or the other way around.
        /// </summary>
        public string? RequestScheme { get; init; }

        /// <param name="configuration">The proxy, for its header rules.</param>
        /// <param name="fromClient">True when the bytes are from the client (requests), false when they are from the server (responses).</param>
        public Http2StreamParser(NpProxyConfiguration configuration, bool fromClient)
        {
            _configuration = configuration;
            _fromClient = fromClient;
            _state = fromClient ? ParserState.Preface : ParserState.FrameHeader;
        }

        public (byte[] Bytes, int Length) Process(byte[] bytes, int length)
        {
            var output = new MemoryStream(length + 64);

            //Fast path: the payload of a frame that is being passed on, with nothing held back.
            if (_state == ParserState.Payload && _start == _end && !_collecting && _payloadRemaining >= length)
            {
                output.Write(bytes, 0, length);
                _payloadRemaining -= length;
                if (_payloadRemaining == 0)
                {
                    _state = ParserState.FrameHeader;
                }
                return (output.GetBuffer(), (int)output.Length);
            }

            Append(bytes, length);

            while (Step(output))
            {
            }

            return (output.GetBuffer(), (int)output.Length);
        }

        private int Available => _end - _start;

        private void Append(byte[] bytes, int length)
        {
            if (_buffer.Length - _end < length)
            {
                int count = Available;
                if (_start > 0)
                {
                    Buffer.BlockCopy(_buffer, _start, _buffer, 0, count);
                    _start = 0;
                    _end = count;
                }

                if (_buffer.Length - _end < length)
                {
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _end + length));
                }
            }

            Buffer.BlockCopy(bytes, 0, _buffer, _end, length);
            _end += length;
        }

        private void Consume(int count)
        {
            _start += count;
            if (_start == _end)
            {
                _start = _end = 0;
            }
        }

        /// <summary>
        /// Does the next piece of work, returning false when more bytes are needed.
        /// </summary>
        private bool Step(MemoryStream output)
        {
            switch (_state)
            {
                case ParserState.Preface:
                    {
                        if (Available < ClientPreface.Length)
                        {
                            //Reject a stream that cannot be HTTP/2 as soon as that is known.
                            if (!_buffer.AsSpan(_start, Available).SequenceEqual(ClientPreface.AsSpan(0, Available)))
                            {
                                throw new InvalidDataException("The client did not start with the HTTP/2 connection preface.");
                            }
                            return false;
                        }

                        if (!_buffer.AsSpan(_start, ClientPreface.Length).SequenceEqual(ClientPreface))
                        {
                            throw new InvalidDataException("The client did not start with the HTTP/2 connection preface.");
                        }

                        output.Write(_buffer, _start, ClientPreface.Length);
                        Consume(ClientPreface.Length);
                        _state = ParserState.FrameHeader;
                        return true;
                    }

                case ParserState.FrameHeader:
                    {
                        if (Available < FrameHeaderLength)
                        {
                            return false;
                        }

                        var header = _buffer.AsSpan(_start, FrameHeaderLength);
                        int payloadLength = (header[0] << 16) | (header[1] << 8) | header[2];
                        byte type = header[3];
                        byte flags = header[4];
                        uint streamId = BinaryPrimitives.ReadUInt32BigEndian(header[5..]) & 0x7FFFFFFF;

                        if (_collecting && (type != FrameContinuation || streamId != _blockStreamId))
                        {
                            throw new InvalidDataException("A header block was interrupted by another frame.");
                        }

                        if (type == FrameHeaders || type == FramePushPromise || type == FrameContinuation)
                        {
                            if (type == FrameContinuation && !_collecting)
                            {
                                throw new InvalidDataException("A CONTINUATION frame arrived without a header block.");
                            }

                            if (payloadLength > MaxHeaderBlockBytes)
                            {
                                throw new InvalidDataException("A header frame is too large.");
                            }

                            //Frames that carry header blocks are handled whole.
                            if (Available < FrameHeaderLength + payloadLength)
                            {
                                return false;
                            }

                            var payload = _buffer.AsSpan(_start + FrameHeaderLength, payloadLength).ToArray();
                            Consume(FrameHeaderLength + payloadLength);
                            HandleHeaderFrame(output, type, flags, streamId, payload);
                            return true;
                        }

                        //Any other frame is passed on as it arrives.
                        TrackStreamEnd(type, flags, streamId);
                        output.Write(header);
                        Consume(FrameHeaderLength);

                        _payloadRemaining = payloadLength;
                        _state = payloadLength > 0 ? ParserState.Payload : ParserState.FrameHeader;
                        return true;
                    }

                default:
                    {
                        int count = (int)Math.Min(Available, _payloadRemaining);
                        if (count == 0)
                        {
                            return false;
                        }

                        output.Write(_buffer, _start, count);
                        Consume(count);
                        _payloadRemaining -= count;

                        if (_payloadRemaining == 0)
                        {
                            _state = ParserState.FrameHeader;
                        }
                        return true;
                    }
            }
        }

        /// <summary>
        /// A stream that has ended, or been reset, has no more header blocks that could be "the first".
        /// </summary>
        private void TrackStreamEnd(byte type, byte flags, uint streamId)
        {
            if ((type == FrameData && (flags & FlagEndStream) != 0) || type == FrameRstStream)
            {
                _streamsWithHeaders.Remove(streamId);
            }
        }

        private void HandleHeaderFrame(MemoryStream output, byte type, byte flags, uint streamId, byte[] payload)
        {
            if (type == FrameContinuation)
            {
                _block.Write(payload);
            }
            else
            {
                int position = 0;
                int padding = 0;

                if ((flags & FlagPadded) != 0)
                {
                    padding = payload.Length > 0 ? payload[position++] : throw new InvalidDataException("A padded frame has no padding length.");
                }

                _blockPromisedStream = null;
                _blockPriority = null;

                if (type == FramePushPromise)
                {
                    if (payload.Length - position < 4)
                    {
                        throw new InvalidDataException("A PUSH_PROMISE frame is too short.");
                    }
                    _blockPromisedStream = payload[position..(position + 4)];
                    position += 4;
                }
                else if ((flags & FlagPriority) != 0)
                {
                    if (payload.Length - position < 5)
                    {
                        throw new InvalidDataException("A HEADERS frame is too short.");
                    }
                    _blockPriority = payload[position..(position + 5)];
                    position += 5;
                }

                int fragmentLength = payload.Length - position - padding;
                if (fragmentLength < 0)
                {
                    throw new InvalidDataException("A frame has more padding than data.");
                }

                _block.SetLength(0);
                _block.Write(payload, position, fragmentLength);

                _blockFrameType = type;
                _blockStreamId = streamId;
                _blockEndStream = type == FrameHeaders && (flags & FlagEndStream) != 0;
                _collecting = true;
            }

            if (_block.Length > MaxHeaderBlockBytes)
            {
                throw new InvalidDataException("A header block is too large.");
            }

            if ((flags & FlagEndHeaders) != 0)
            {
                _collecting = false;
                CompleteHeaderBlock(output);
            }
        }

        private void CompleteHeaderBlock(MemoryStream output)
        {
            //Decode every block, in order, to keep the dynamic table in step with the sender.
            var fields = _decoder.Decode(_block.GetBuffer().AsSpan(0, (int)_block.Length));

            if (_blockFrameType == FrameHeaders)
            {
                ApplyRules(fields);

                if (_blockEndStream)
                {
                    _streamsWithHeaders.Remove(_blockStreamId);
                }
            }

            var encoded = HpackEncoder.Encode(fields);
            WriteHeaderBlock(output, encoded);
        }

        /// <summary>
        /// Applies the rules to the first header block of a stream, which is the request or the (final) response. Later blocks are
        /// trailers, and a response with a 1xx status is informational and is followed by the real one.
        /// </summary>
        private void ApplyRules(List<HeaderField> fields)
        {
            if (_streamsWithHeaders.Count > 100_000)
            {
                _streamsWithHeaders.Clear(); //Streams that were never ended cleanly. Bounds the memory they use.
            }

            if (!_streamsWithHeaders.Add(_blockStreamId))
            {
                return; //Trailers.
            }

            if (_fromClient)
            {
                if (RequestScheme != null)
                {
                    foreach (var field in fields.Where(f => f.NameText == ":scheme"))
                    {
                        field.Value = Encoding.ASCII.GetBytes(RequestScheme);
                    }
                }

                var method = fields.FirstOrDefault(f => f.NameText == ":method")?.ValueText ?? string.Empty;
                var rules = HttpUtility.GetApplicableRules(_configuration, HttpHeaderType.Request, method);

                Http2HeaderRules.Apply(fields, rules, isRequest: true);
            }
            else
            {
                var status = fields.FirstOrDefault(f => f.NameText == ":status")?.ValueText ?? string.Empty;
                if (status.StartsWith('1'))
                {
                    _streamsWithHeaders.Remove(_blockStreamId); //Informational: the final response is still to come.
                    return;
                }

                //A response carries no method, so only rules for any verb apply.
                var rules = HttpUtility.GetApplicableRules(_configuration, HttpHeaderType.Response, string.Empty);

                Http2HeaderRules.Apply(fields, rules, isRequest: false);
            }
        }

        /// <summary>
        /// Writes a header block as a HEADERS (or PUSH_PROMISE) frame followed by as many CONTINUATION frames as it needs.
        /// </summary>
        private void WriteHeaderBlock(MemoryStream output, byte[] block)
        {
            var prefix = _blockFrameType == FramePushPromise ? _blockPromisedStream : _blockPriority;
            int prefixLength = prefix?.Length ?? 0;

            int firstFragment = Math.Min(block.Length, DefaultMaxFramePayload - prefixLength);
            bool single = firstFragment == block.Length;

            byte flags = single ? FlagEndHeaders : (byte)0;
            if (_blockFrameType == FrameHeaders)
            {
                if (_blockEndStream)
                {
                    flags |= FlagEndStream;
                }
                if (_blockPriority != null)
                {
                    flags |= FlagPriority;
                }
            }

            WriteFrameHeader(output, prefixLength + firstFragment, _blockFrameType, flags, _blockStreamId);
            if (prefix != null)
            {
                output.Write(prefix);
            }
            output.Write(block, 0, firstFragment);

            int offset = firstFragment;
            while (offset < block.Length)
            {
                int count = Math.Min(DefaultMaxFramePayload, block.Length - offset);
                bool last = offset + count == block.Length;

                WriteFrameHeader(output, count, FrameContinuation, last ? FlagEndHeaders : (byte)0, _blockStreamId);
                output.Write(block, offset, count);
                offset += count;
            }
        }

        private static void WriteFrameHeader(MemoryStream output, int payloadLength, byte type, byte flags, uint streamId)
        {
            Span<byte> header = stackalloc byte[FrameHeaderLength];
            header[0] = (byte)(payloadLength >> 16);
            header[1] = (byte)(payloadLength >> 8);
            header[2] = (byte)payloadLength;
            header[3] = type;
            header[4] = flags;
            BinaryPrimitives.WriteUInt32BigEndian(header[5..], streamId & 0x7FFFFFFF);
            output.Write(header);
        }
    }
}
