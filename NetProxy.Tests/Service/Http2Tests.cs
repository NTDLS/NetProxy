using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Http;
using NetProxy.Service.Http.Http2;
using NetProxy.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class HpackTests
    {
        private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

        [Fact]
        public void The_decoder_reads_the_first_example_of_the_specification_without_huffman_coding()
        {
            //RFC 7541, C.3.1
            var fields = new HpackDecoder().Decode(Hex("8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d"));

            Assert.Equal([(":method", "GET"), (":scheme", "http"), (":path", "/"), (":authority", "www.example.com")],
                fields.Select(o => (o.NameText, o.ValueText)));
        }

        [Fact]
        public void The_decoder_reads_the_first_example_of_the_specification_with_huffman_coding()
        {
            //RFC 7541, C.4.1
            var fields = new HpackDecoder().Decode(Hex("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff"));

            Assert.Equal("www.example.com", fields.Single(o => o.NameText == ":authority").ValueText);
        }

        [Fact]
        public void The_decoder_keeps_its_table_between_the_blocks_of_a_connection()
        {
            //RFC 7541, C.3.1 and C.3.2: the second request refers to the :authority that the first one added to the table.
            var decoder = new HpackDecoder();
            decoder.Decode(Hex("8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d"));
            var second = decoder.Decode(Hex("8286 84be 5808 6e6f 2d63 6163 6865"));

            Assert.Equal("www.example.com", second.Single(o => o.NameText == ":authority").ValueText);
            Assert.Equal("no-cache", second.Single(o => o.NameText == "cache-control").ValueText);
        }

        [Fact]
        public void The_encoder_and_decoder_agree()
        {
            var fields = new List<HeaderField>
            {
                new(":method", "GET"), new(":path", "/some/path?query=1"), new(":authority", "example.com"),
                new("x-custom", "value with spaces"), new("x-long", new string('z', 5000)),
                new("authorization", "Bearer secret") ,
                new("x-binary", "éÿ\u0001")
            };
            fields[5] = new HeaderField(Encoding.Latin1.GetBytes("authorization"), Encoding.Latin1.GetBytes("Bearer secret"), neverIndex: true);

            var decoded = new HpackDecoder().Decode(HpackEncoder.Encode(fields));

            Assert.Equal(fields.Select(o => (o.NameText, o.ValueText)), decoded.Select(o => (o.NameText, o.ValueText)));
            Assert.True(decoded.Single(o => o.NameText == "authorization").NeverIndex);
        }

        [Fact]
        public void The_decoder_refuses_a_block_that_makes_no_sense()
        {
            Assert.Throws<HpackException>(() => new HpackDecoder().Decode(Hex("80")));                 //Index 0.
            Assert.Throws<HpackException>(() => new HpackDecoder().Decode(Hex("ff ff ff ff ff ff 7f"))); //An index far past the table.
        }
    }

    public class Http2ParserTests
    {
        private static readonly byte[] Preface = Http2StreamParser.ClientPrefaceBytes;

        private static byte[] Frame(byte type, byte flags, uint streamId, byte[] payload)
        {
            var frame = new byte[9 + payload.Length];
            frame[0] = (byte)(payload.Length >> 16);
            frame[1] = (byte)(payload.Length >> 8);
            frame[2] = (byte)payload.Length;
            frame[3] = type;
            frame[4] = flags;
            frame[5] = (byte)(streamId >> 24);
            frame[6] = (byte)(streamId >> 16);
            frame[7] = (byte)(streamId >> 8);
            frame[8] = (byte)streamId;
            payload.CopyTo(frame, 9);
            return frame;
        }

        private const byte Data = 0, Headers = 1, Settings = 4, Continuation = 9;
        private const byte EndStream = 1, EndHeaders = 4;

        private static List<(byte Type, byte Flags, uint StreamId, byte[] Payload)> Frames(byte[] bytes)
        {
            var frames = new List<(byte, byte, uint, byte[])>();
            var position = 0;

            while (position + 9 <= bytes.Length)
            {
                var length = (bytes[position] << 16) | (bytes[position + 1] << 8) | bytes[position + 2];
                var streamId = ((uint)bytes[position + 5] << 24) | ((uint)bytes[position + 6] << 16) | ((uint)bytes[position + 7] << 8) | bytes[position + 8];
                frames.Add((bytes[position + 3], bytes[position + 4], streamId, bytes[(position + 9)..(position + 9 + length)]));
                position += 9 + length;
            }

            Assert.Equal(bytes.Length, position); //Nothing is left over: the output is whole frames.
            return frames;
        }

        private static byte[] Concatenate(params byte[][] parts) => parts.SelectMany(o => o).ToArray();

        private static byte[] Run(Http2StreamParser parser, byte[] input, int pieceSize = int.MaxValue)
        {
            var output = new MemoryStream();

            for (var offset = 0; offset < input.Length; offset += pieceSize)
            {
                var piece = input[offset..Math.Min(input.Length, offset + pieceSize)];
                var (bytes, length) = parser.Process(piece, piece.Length);
                output.Write(bytes, 0, length);
            }

            return output.ToArray();
        }

        private static List<HeaderField> RequestFields() =>
        [
            new(":method", "GET"), new(":scheme", "https"), new(":path", "/"), new(":authority", "example.com"), new("accept", "*/*")
        ];

        private static NpProxyConfiguration Configuration(params NpHttpHeaderRule[] rules)
        {
            var configuration = ProxyBuilder.Configuration("p", 443, TrafficType.Https, 9000);
            foreach (var rule in rules)
            {
                configuration.HttpHeaderRules.Add(rule);
            }

            return configuration;
        }

        private static Dictionary<string, string> RequestHeaders(byte[] output)
        {
            var frames = Frames(output[Preface.Length..]);
            var headers = frames.Single(o => o.Type == Headers);
            return new HpackDecoder().Decode(headers.Payload).ToDictionary(o => o.NameText, o => o.ValueText);
        }

        [Fact]
        public void The_preface_and_frames_without_rules_come_out_the_way_they_went_in()
        {
            var input = Concatenate(Preface, Frame(Settings, 0, 0, []),
                Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(RequestFields())));

            var output = Run(new Http2StreamParser(Configuration(), fromClient: true), input);

            var headers = RequestHeaders(output);
            Assert.Equal("example.com", headers[":authority"]);
            Assert.Equal("*/*", headers["accept"]);
        }

        [Fact]
        public void A_rule_adds_a_header_to_a_request()
        {
            var input = Concatenate(Preface, Frame(Settings, 0, 0, []),
                Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(RequestFields())));
            var rule = new NpHttpHeaderRule { Name = "X-Test", Value = "1", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Request, Enabled = true };

            var headers = RequestHeaders(Run(new Http2StreamParser(Configuration(rule), fromClient: true), input));

            Assert.Equal("1", headers["x-test"]);
        }

        [Fact]
        public void A_rule_for_host_changes_the_authority()
        {
            var input = Concatenate(Preface, Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(RequestFields())));
            var rule = new NpHttpHeaderRule { Name = "Host", Value = "internal.local", Action = HttpHeaderAction.Update, HeaderType = HttpHeaderType.Request, Enabled = true };

            var headers = RequestHeaders(Run(new Http2StreamParser(Configuration(rule), fromClient: true), input));

            Assert.Equal("internal.local", headers[":authority"]);
        }

        [Fact]
        public void The_scheme_is_changed_when_the_endpoint_connection_differs()
        {
            var input = Concatenate(Preface, Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(RequestFields())));

            var headers = RequestHeaders(Run(new Http2StreamParser(Configuration(), fromClient: true) { RequestScheme = "http" }, input));

            Assert.Equal("http", headers[":scheme"]);
        }

        [Fact]
        public void The_forwarded_headers_are_added_to_requests()
        {
            var input = Concatenate(Preface, Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(RequestFields())));
            var forwarded = new NpForwardedHeaders(ForwardedHeadersMode.Replace, "203.0.113.9", "https");

            var headers = RequestHeaders(Run(new Http2StreamParser(Configuration(), fromClient: true) { Forwarded = forwarded }, input));

            Assert.Equal("203.0.113.9", headers["x-forwarded-for"]);
            Assert.Equal("https", headers["x-forwarded-proto"]);
        }

        [Fact]
        public void A_header_block_in_several_frames_is_put_together_and_changed()
        {
            var block = HpackEncoder.Encode(RequestFields());
            var input = Concatenate(Preface,
                Frame(Headers, EndStream, 1, block[..5]),
                Frame(Continuation, EndHeaders, 1, block[5..]));
            var rule = new NpHttpHeaderRule { Name = "X-Test", Value = "1", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Request, Enabled = true };

            var output = Run(new Http2StreamParser(Configuration(rule), fromClient: true), input);
            var headers = RequestHeaders(output);

            Assert.Equal("1", headers["x-test"]);
            Assert.Equal("example.com", headers[":authority"]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        public void The_result_does_not_depend_on_how_the_bytes_arrive(int pieceSize)
        {
            var input = Concatenate(Preface, Frame(Settings, 0, 0, []),
                Frame(Headers, EndHeaders, 1, HpackEncoder.Encode(RequestFields())),
                Frame(Data, EndStream, 1, Encoding.ASCII.GetBytes("body")));
            var rule = new NpHttpHeaderRule { Name = "X-Test", Value = "1", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Request, Enabled = true };

            var whole = Run(new Http2StreamParser(Configuration(rule), fromClient: true), input);
            var pieces = Run(new Http2StreamParser(Configuration(rule), fromClient: true), input, pieceSize);

            Assert.Equal(whole, pieces);
            Assert.Equal("body", Encoding.ASCII.GetString(Frames(whole[Preface.Length..]).Single(o => o.Type == Data).Payload));
        }

        [Fact]
        public void Data_frames_are_passed_on_untouched()
        {
            var payload = Enumerable.Range(0, 200).Select(o => (byte)o).ToArray();
            var input = Concatenate(Preface, Frame(Headers, EndHeaders, 1, HpackEncoder.Encode(RequestFields())), Frame(Data, EndStream, 1, payload));

            var output = Run(new Http2StreamParser(Configuration(), fromClient: true), input);

            Assert.Equal(payload, Frames(output[Preface.Length..]).Single(o => o.Type == Data).Payload);
        }

        [Fact]
        public void A_rule_for_responses_changes_the_response_headers()
        {
            var response = new List<HeaderField> { new(":status", "200"), new("content-type", "text/plain") };
            var input = Frame(Headers, EndHeaders | EndStream, 1, HpackEncoder.Encode(response));
            var rule = new NpHttpHeaderRule { Name = "X-Resp", Value = "yes", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Response, Enabled = true };

            var output = Run(new Http2StreamParser(Configuration(rule), fromClient: false), input);

            var headers = new HpackDecoder().Decode(Frames(output).Single(o => o.Type == Headers).Payload).ToDictionary(o => o.NameText, o => o.ValueText);
            Assert.Equal("yes", headers["x-resp"]);
            Assert.Equal("200", headers[":status"]);
        }
    }
}
