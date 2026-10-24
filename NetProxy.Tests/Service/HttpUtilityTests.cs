using NetProxy.Library;
using NetProxy.Service.Http;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class HttpUtilityTests
    {
        [Theory]
        [InlineData("GET / HTTP/1.1\r\nHost: x\r\n\r\n", HttpHeaderType.Request, "GET")]
        [InlineData("post /a HTTP/1.0\r\n\r\n", HttpHeaderType.Request, "POST")]
        [InlineData("OPTIONS * HTTP/1.1\r\n\r\n", HttpHeaderType.Request, "OPTIONS")]
        [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", HttpHeaderType.Response, null)]
        [InlineData("HTTP/2 404 Not Found\r\n\r\n", HttpHeaderType.Response, null)]
        public void The_first_line_tells_a_request_from_a_response(string text, HttpHeaderType expected, string? verb)
        {
            Assert.Equal(expected, HttpUtility.IsHttpHeader(text, out var found));

            if (verb != null)
            {
                Assert.Equal(verb, found, ignoreCase: true);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("hello world\r\n")]
        [InlineData("\u0016\u0003\u0001\u0002\u0000\u0001\u0000\u0001ü\u0003\u0003")]
        [InlineData("GETX / HTTP/1.1\r\n")]
        public void Anything_else_is_not_http(string text)
            => Assert.Equal(HttpHeaderType.None, HttpUtility.IsHttpHeader(text, out _));

        [Fact]
        public void Bytes_are_classified_like_text()
        {
            var bytes = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
            Assert.Equal(HttpHeaderType.Request, HttpUtility.IsHttpHeader(bytes, bytes.Length, out _));
            Assert.Equal(HttpHeaderType.None, HttpUtility.IsHttpHeader((byte[]?)null, 0, out _));
        }

        [Theory]
        [InlineData("GET / HTTP/1.1\r\nHost: x\r\n\r\nbody", "\r\n", 27)]
        [InlineData("GET / HTTP/1.1\nHost: x\n\nbody", "\n", 24)]
        [InlineData("GET / HTTP/1.1\r\nHost: x\r\n", "", -1)]
        public void The_end_of_the_header_is_found_for_either_line_break(string text, string delimiter, int end)
        {
            Assert.Equal(end, HttpUtility.GetHttpHeaderEnd(text, out var found));
            Assert.Equal(delimiter, found);
        }

        [Fact]
        public void Upsert_updates_a_header_that_is_there_and_adds_one_that_is_not()
        {
            const string header = "GET / HTTP/1.1\r\nHost: x\r\nX-A: 1\r\n\r\n";

            Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\nX-A: 2\r\n\r\n", HttpUtility.UpsertHttpHostHeaderValue(header, "X-A", "2", "\r\n"));
            Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\nX-A: 1\r\nX-B: 2\r\n\r\n", HttpUtility.UpsertHttpHostHeaderValue(header, "X-B", "2", "\r\n"));
        }

        [Fact]
        public void Header_names_are_not_case_sensitive_and_only_whole_names_match()
        {
            const string header = "GET / HTTP/1.1\r\nx-a: 1\r\nX-AB: keep\r\n\r\n";

            var output = HttpUtility.UpdateHttpHostHeaderValue(header, "X-A", "2", "\r\n");

            Assert.Contains("X-A: 2", output);
            Assert.Contains("X-AB: keep", output);
        }

        [Fact]
        public void Insert_leaves_a_header_that_is_there_alone()
        {
            const string header = "GET / HTTP/1.1\r\nX-A: 1\r\n\r\n";

            Assert.Equal(header, HttpUtility.InsertHttpHostHeaderValue(header, "X-A", "2", "\r\n"));
            Assert.Contains("X-B: 2", HttpUtility.InsertHttpHostHeaderValue(header, "X-B", "2", "\r\n"));
        }

        [Fact]
        public void Update_does_nothing_for_a_header_that_is_not_there()
        {
            const string header = "GET / HTTP/1.1\r\nX-A: 1\r\n\r\n";
            Assert.Equal(header, HttpUtility.UpdateHttpHostHeaderValue(header, "X-B", "2", "\r\n"));
        }

        [Fact]
        public void Delete_removes_the_whole_line()
        {
            Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\n\r\n",
                HttpUtility.DeleteHttpHostHeaderValue("GET / HTTP/1.1\r\nHost: x\r\nX-A: 1\r\n\r\n", "X-A", "\r\n"));
        }

        [Fact]
        public void A_value_with_special_characters_is_written_as_it_is()
        {
            var output = HttpUtility.UpsertHttpHostHeaderValue("GET / HTTP/1.1\r\nX-A: 1\r\n\r\n", "X-A", "$1 \\ ${x} (a|b)", "\r\n");
            Assert.Contains("X-A: $1 \\ ${x} (a|b)", output);
        }
    }
}
