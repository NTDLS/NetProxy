using NetProxy.Library;
using NetProxy.Service.Http;
using NetProxy.Service.Http.Http2;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class ForwardedHeadersTests
    {
        private static NpForwardedHeaders Replace(string scheme = "https") => new(ForwardedHeadersMode.Replace, "203.0.113.9", scheme);
        private static NpForwardedHeaders Append(string scheme = "https") => new(ForwardedHeadersMode.Append, "203.0.113.9", scheme);

        private const string Plain = "GET / HTTP/1.1\r\nHost: x\r\n\r\n";

        [Fact]
        public void Replace_adds_both_headers_before_the_blank_line()
            => Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\nX-Forwarded-For: 203.0.113.9\r\nX-Forwarded-Proto: https\r\n\r\n",
                Replace().ApplyToHttp1Request(Plain, "\r\n"));

        [Fact]
        public void Replace_overwrites_what_the_client_sent_whatever_the_case_of_the_name()
        {
            var output = Replace("http").ApplyToHttp1Request("GET / HTTP/1.1\r\nHost: x\r\nx-forwarded-for: 6.6.6.6\r\nX-FORWARDED-PROTO: https\r\n\r\n", "\r\n");

            Assert.DoesNotContain("6.6.6.6", output);
            Assert.Contains("X-Forwarded-For: 203.0.113.9", output);
            Assert.Contains("X-Forwarded-Proto: http", output);
            Assert.Equal(1, output.Split("orwarded-For", StringSplitOptions.None).Length - 1 + 0 * 1 + 0); //One line for the address.
        }

        [Fact]
        public void Append_adds_the_client_to_the_chain_and_keeps_the_proto()
        {
            var output = Append("http").ApplyToHttp1Request("GET / HTTP/1.1\r\nHost: x\r\nX-Forwarded-For: 10.0.0.1, 10.0.0.2\r\nX-Forwarded-Proto: https\r\n\r\n", "\r\n");

            Assert.Contains("X-Forwarded-For: 10.0.0.1, 10.0.0.2, 203.0.113.9", output);
            Assert.Contains("X-Forwarded-Proto: https", output);
            Assert.DoesNotContain("X-Forwarded-Proto: http\r", output);
        }

        [Fact]
        public void Append_starts_a_chain_when_there_is_none_or_it_is_empty()
        {
            Assert.Contains("X-Forwarded-For: 203.0.113.9\r\n", Append().ApplyToHttp1Request(Plain, "\r\n"));
            Assert.Contains("X-Forwarded-For: 203.0.113.9\r\n",
                Append().ApplyToHttp1Request("GET / HTTP/1.1\r\nHost: x\r\nX-Forwarded-For: \r\n\r\n", "\r\n"));
        }

        [Fact]
        public void Append_adds_the_proto_when_there_is_none()
            => Assert.Contains("X-Forwarded-Proto: https", Append().ApplyToHttp1Request(Plain, "\r\n"));

        [Fact]
        public void The_header_stays_one_block_ending_in_one_blank_line()
        {
            var output = Replace().ApplyToHttp1Request(Plain, "\r\n");

            Assert.EndsWith("\r\n\r\n", output);
            Assert.DoesNotContain("\r\n\r\n\r\n", output);
        }

        [Fact]
        public void Bare_line_feeds_are_kept()
        {
            var output = Replace().ApplyToHttp1Request("GET / HTTP/1.1\nHost: x\n\n", "\n");

            Assert.Contains("X-Forwarded-For: 203.0.113.9\n", output);
            Assert.DoesNotContain("\r", output);
        }

        [Fact]
        public void An_ipv6_client_is_written_as_it_is()
            => Assert.Contains("X-Forwarded-For: 2001:db8::1",
                new NpForwardedHeaders(ForwardedHeadersMode.Replace, "2001:db8::1", "https").ApplyToHttp1Request(Plain, "\r\n"));

        //---------------------------------------------------------------- HTTP/2

        private static List<HeaderField> Fields(params (string Name, string Value)[] fields)
            => fields.Select(o => new HeaderField(o.Name, o.Value)).ToList();

        private static string Value(List<HeaderField> fields, string name)
            => string.Join("|", fields.Where(o => o.NameText == name).Select(o => o.ValueText));

        [Fact]
        public void Http2_replace_sets_both_and_drops_what_the_client_sent()
        {
            var fields = Fields((":method", "GET"), ("x-forwarded-for", "6.6.6.6"), ("x-forwarded-proto", "http"), ("accept", "*/*"));

            Replace().ApplyToHttp2Request(fields);

            Assert.Equal("203.0.113.9", Value(fields, "x-forwarded-for"));
            Assert.Equal("https", Value(fields, "x-forwarded-proto"));
            Assert.Equal("*/*", Value(fields, "accept"));
            Assert.Equal("GET", Value(fields, ":method"));
        }

        [Fact]
        public void Http2_append_extends_the_chain_and_keeps_the_proto()
        {
            var fields = Fields((":method", "GET"), ("x-forwarded-for", "10.0.0.1"), ("x-forwarded-proto", "https"));

            Append("http").ApplyToHttp2Request(fields);

            Assert.Equal("10.0.0.1, 203.0.113.9", Value(fields, "x-forwarded-for"));
            Assert.Equal("https", Value(fields, "x-forwarded-proto"));
        }

        [Fact]
        public void Http2_append_without_a_chain_adds_both()
        {
            var fields = Fields((":method", "GET"));

            Append().ApplyToHttp2Request(fields);

            Assert.Equal("203.0.113.9", Value(fields, "x-forwarded-for"));
            Assert.Equal("https", Value(fields, "x-forwarded-proto"));
        }

        [Fact]
        public void Http2_names_are_written_in_lower_case()
        {
            var fields = Fields((":method", "GET"));
            Replace().ApplyToHttp2Request(fields);

            Assert.All(fields, f => Assert.Equal(f.NameText.ToLowerInvariant(), f.NameText));
        }
    }
}
