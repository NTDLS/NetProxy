using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Http;
using NetProxy.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class HttpStreamParserTests
    {
        private static NpProxyConfiguration WithRules(params NpHttpHeaderRule[] rules)
        {
            var configuration = ProxyBuilder.Configuration("p", 80, TrafficType.Http, 9000);
            foreach (var rule in rules)
            {
                configuration.HttpHeaderRules.Add(rule);
            }

            return configuration;
        }

        private static NpHttpHeaderRule Rule(string name, string value, HttpHeaderAction action = HttpHeaderAction.Upsert,
            HttpHeaderType type = HttpHeaderType.Request, HttpVerb verb = HttpVerb.Any, bool enabled = true)
            => new() { Name = name, Value = value, Action = action, HeaderType = type, Verb = verb, Enabled = enabled };

        /// <summary>
        /// What comes out of the parser when the chunks go in one after the other, the way reads arrive.
        /// </summary>
        private static string Feed(IHttpStreamProcessor parser, params string[] chunks)
        {
            var output = new StringBuilder();

            foreach (var chunk in chunks)
            {
                var bytes = Encoding.Latin1.GetBytes(chunk);
                var (result, length) = parser.Process(bytes, bytes.Length);
                output.Append(Encoding.Latin1.GetString(result, 0, length));
            }

            return output.ToString();
        }

        private static HttpStreamParser Requests(NpProxyConfiguration configuration, NpForwardedHeaders? forwarded = null)
            => new(configuration, ConnectionDirection.Inbound, forwarded);

        private static HttpStreamParser Responses(NpProxyConfiguration configuration)
            => new(configuration, ConnectionDirection.Outbound);

        [Fact]
        public void A_rule_adds_a_header_to_a_request_and_nothing_else_changes()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))), "GET /a?b=c HTTP/1.1\r\nHost: x\r\nAccept: */*\r\n\r\n");

            Assert.Equal("GET /a?b=c HTTP/1.1\r\nHost: x\r\nAccept: */*\r\nX-Test: 1\r\n\r\n", output);
        }

        [Fact]
        public void A_header_that_arrives_in_pieces_is_held_until_it_is_whole()
        {
            var parser = Requests(WithRules(Rule("X-Test", "1")));

            var first = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHo");
            Assert.Equal(0, parser.Process(first, first.Length).Length);

            var output = Feed(parser, "st: x\r\n", "\r\n");
            Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\nX-Test: 1\r\n\r\n", output);
        }

        [Fact]
        public void A_header_split_in_the_middle_of_the_line_break_is_found()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))), "GET / HTTP/1.1\r\nHost: x\r", "\n\r", "\n");

            Assert.Equal("GET / HTTP/1.1\r\nHost: x\r\nX-Test: 1\r\n\r\n", output);
        }

        [Fact]
        public void Every_request_on_a_keep_alive_connection_gets_the_rule()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))),
                "GET /1 HTTP/1.1\r\nHost: x\r\n\r\n",
                "GET /2 HTTP/1.1\r\nHost: x\r\n\r\nGET /3 HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Equal(3, output.Split("X-Test: 1").Length - 1);
        }

        [Fact]
        public void A_body_that_looks_like_a_request_is_not_changed()
        {
            const string body = "GET /evil HTTP/1.1\r\nHost: y\r\n\r\n";
            var request = $"POST / HTTP/1.1\r\nHost: x\r\nContent-Length: {body.Length}\r\n\r\n{body}";

            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))), request, "GET /next HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Contains(body, output);
            Assert.Equal(2, output.Split("X-Test: 1").Length - 1); //The post and the next request, not the body.
        }

        [Fact]
        public void A_body_that_arrives_in_pieces_is_passed_on_whole()
        {
            const string body = "0123456789";
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))),
                $"POST / HTTP/1.1\r\nHost: x\r\nContent-Length: {body.Length}\r\n\r\n01234", "56789", "GET /n HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Contains("\r\n\r\n0123456789GET /n", output);
        }

        [Fact]
        public void A_chunked_body_is_followed_and_the_next_request_is_found()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))),
                "POST / HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nGET /\r\n0\r\n\r\n",
                "GET /next HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Equal(2, output.Split("X-Test: 1").Length - 1);
            Assert.Contains("5\r\nGET /\r\n0\r\n\r\n", output);
        }

        [Fact]
        public void A_chunked_body_split_in_awkward_places_is_followed()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))),
                "POST / HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\n", "5\r", "\nhel", "lo\r", "\n0\r\n", "\r\n",
                "GET /next HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Equal(2, output.Split("X-Test: 1").Length - 1);
            Assert.Contains("hello", output);
        }

        [Fact]
        public void A_rule_for_responses_applies_to_responses_only()
        {
            var configuration = WithRules(Rule("X-Resp", "1", type: HttpHeaderType.Response));

            Assert.Equal("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nX-Resp: 1\r\n\r\nok", Feed(Responses(configuration), "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"));
            Assert.DoesNotContain("X-Resp", Feed(Requests(configuration), "GET / HTTP/1.1\r\nHost: x\r\n\r\n"));
        }

        [Fact]
        public void A_rule_for_a_verb_applies_to_that_verb_only()
        {
            var parser = Requests(WithRules(Rule("X-Test", "1", verb: HttpVerb.Post)));

            Assert.DoesNotContain("X-Test", Feed(parser, "GET / HTTP/1.1\r\nHost: x\r\n\r\n"));
            Assert.Contains("X-Test: 1", Feed(parser, "POST / HTTP/1.1\r\nHost: x\r\nContent-Length: 0\r\n\r\n"));
        }

        [Fact]
        public void A_disabled_rule_does_nothing()
            => Assert.DoesNotContain("X-Test", Feed(Requests(WithRules(Rule("X-Test", "1", enabled: false))), "GET / HTTP/1.1\r\nHost: x\r\n\r\n"));

        [Fact]
        public void Rules_update_insert_and_delete()
        {
            var configuration = WithRules(
                Rule("X-Update", "new", HttpHeaderAction.Update),
                Rule("X-Insert", "new", HttpHeaderAction.Insert),
                Rule("X-Delete", "", HttpHeaderAction.Delete),
                Rule("X-Missing", "new", HttpHeaderAction.Update));

            var output = Feed(Requests(configuration), "GET / HTTP/1.1\r\nHost: x\r\nX-Update: old\r\nX-Insert: keep\r\nX-Delete: gone\r\n\r\n");

            Assert.Contains("X-Update: new", output);
            Assert.Contains("X-Insert: keep", output);
            Assert.DoesNotContain("X-Delete", output);
            Assert.DoesNotContain("X-Missing", output);
            Assert.EndsWith("\r\n\r\n", output);
        }

        [Fact]
        public void Data_that_is_not_http_passes_through_unchanged()
        {
            var parser = Requests(WithRules(Rule("X-Test", "1")));

            Assert.Equal("hello world", Feed(parser, "hello world"));
            Assert.Equal("GET / HTTP/1.1\r\n\r\n", Feed(parser, "GET / HTTP/1.1\r\n\r\n")); //Once it is not HTTP it stays that way.
        }

        [Fact]
        public void After_an_upgrade_the_rest_passes_through()
        {
            var parser = Responses(WithRules(Rule("X-Resp", "1", type: HttpHeaderType.Response)));

            var output = Feed(parser, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n", "HTTP/1.1 200 OK\r\n\r\nraw frame");

            Assert.Equal(1, output.Split("X-Resp: 1").Length - 1); //Only the 101 response itself.
            Assert.EndsWith("HTTP/1.1 200 OK\r\n\r\nraw frame", output);
        }

        [Fact]
        public void A_header_written_with_bare_line_feeds_is_found()
        {
            var output = Feed(Requests(WithRules(Rule("X-Test", "1"))), "GET / HTTP/1.1\nHost: x\n\n");

            Assert.Contains("X-Test: 1", output);
        }

        [Fact]
        public void The_forwarded_headers_are_added_to_requests_only()
        {
            var forwarded = new NpForwardedHeaders(ForwardedHeadersMode.Replace, "203.0.113.9", "https");
            var configuration = WithRules();

            var request = Feed(Requests(configuration, forwarded), "GET / HTTP/1.1\r\nHost: x\r\nX-Forwarded-For: 6.6.6.6\r\n\r\n");
            Assert.Contains("X-Forwarded-For: 203.0.113.9", request);
            Assert.DoesNotContain("6.6.6.6", request);
            Assert.Contains("X-Forwarded-Proto: https", request);

            var response = Feed(new HttpStreamParser(configuration, ConnectionDirection.Outbound, forwarded), "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
            Assert.DoesNotContain("X-Forwarded", response);
        }

        [Fact]
        public void A_user_rule_is_applied_after_the_forwarded_headers_so_it_can_change_them()
        {
            var forwarded = new NpForwardedHeaders(ForwardedHeadersMode.Replace, "203.0.113.9", "https");
            var output = Feed(Requests(WithRules(Rule("X-Forwarded-Proto", "custom")), forwarded), "GET / HTTP/1.1\r\nHost: x\r\n\r\n");

            Assert.Contains("X-Forwarded-Proto: custom", output);
            Assert.Equal(1, output.Split("X-Forwarded-Proto").Length - 1);
        }

        [Fact]
        public void A_header_larger_than_the_limit_is_passed_through_rather_than_held_forever()
        {
            var parser = Requests(WithRules(Rule("X-Test", "1")));
            var huge = "GET / HTTP/1.1\r\nHost: x\r\nX-Big: " + new string('a', 70 * 1024);

            var output = Feed(parser, huge);

            Assert.Equal(huge, output);
        }
    }
}
