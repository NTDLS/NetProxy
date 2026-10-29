using NetProxy.PluginApi;
using NetProxy.Plugins.Cache;
using NetProxy.Plugins.Tests.Support;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class CacheTests : PluginTestBase
    {
        private (CachePlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
            => Start(new CachePlugin(Clock), settings);

        private static byte[] Response(string body = "<html>hello</html>", string extraHeaders = "", int status = 200)
            => Http.Bytes($"HTTP/1.1 {status} X\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\n{extraHeaders}\r\n{body}");

        /// <summary>One request on its own connection: the endpoint is asked (a miss), or the cache answers (a hit).</summary>
        private static NpDataEventArgs Get(CachePlugin plugin, FakeConnection connection, string path = "/page", string headers = "", byte[]? answer = null)
        {
            var data = Send(plugin, connection, Http.Request(path, extraHeaders: headers));

            if (data.IsDropped == false && answer != null)
            {
                Send(plugin, connection, answer, direction: NpDataDirection.EndpointToClient);
            }

            return data;
        }

        private static string Body(NpDataEventArgs hit) => Split(hit.Response).Body;

        [Fact]
        public void The_second_request_for_a_page_is_answered_from_the_cache()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Assert.False(Get(plugin, connection, answer: Response()).IsDropped); //A miss: goes to the endpoint, and its answer is kept.

            var hit = Get(plugin, connection);
            Assert.True(hit.IsDropped);
            Assert.False(hit.CloseAfterResponse);
            var (head, body) = Split(hit.Response);
            Assert.StartsWith("HTTP/1.1 200 X\r\n", head);
            Assert.Contains("X-Cache: HIT", head);
            Assert.Contains("Age: 0", head);
            Assert.Contains("Connection: keep-alive", head);
            Assert.Contains("Content-Length: 18", head);
            Assert.Equal("<html>hello</html>", body);
        }

        [Fact]
        public void Another_client_gets_the_kept_page_and_the_age_grows()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), answer: Response());

            Clock.Advance(TimeSpan.FromSeconds(25));
            var hit = Get(plugin, new FakeConnection { ClientAddress = "198.51.100.9" });

            Assert.Contains("Age: 25", Split(hit.Response).Head);
        }

        [Fact]
        public void A_page_is_kept_for_the_default_time_and_then_the_endpoint_is_asked_again()
        {
            var (plugin, _) = Start(new() { ["DefaultTtlSeconds"] = "30" });
            Get(plugin, new FakeConnection(), answer: Response());

            Clock.Advance(TimeSpan.FromSeconds(29));
            Assert.True(Get(plugin, new FakeConnection()).IsDropped);

            Clock.Advance(TimeSpan.FromSeconds(2));
            Assert.False(Get(plugin, new FakeConnection(), answer: Response("<html>new</html>")).IsDropped);
            Assert.Equal("<html>new</html>", Body(Get(plugin, new FakeConnection())));
        }

        [Fact]
        public void The_response_says_how_long_and_s_maxage_wins_but_the_maximum_holds()
        {
            var (plugin, _) = Start(new() { ["MaxTtlSeconds"] = "100" });

            Get(plugin, new FakeConnection(), "/a", answer: Response(extraHeaders: "Cache-Control: public, max-age=10\r\n"));
            Get(plugin, new FakeConnection(), "/b", answer: Response(extraHeaders: "Cache-Control: max-age=10, s-maxage=50\r\n"));
            Get(plugin, new FakeConnection(), "/c", answer: Response(extraHeaders: "Cache-Control: max-age=100000\r\n"));

            Clock.Advance(TimeSpan.FromSeconds(11));
            Assert.False(Get(plugin, new FakeConnection(), "/a").IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), "/b").IsDropped);

            Clock.Advance(TimeSpan.FromSeconds(90));
            Assert.False(Get(plugin, new FakeConnection(), "/c").IsDropped); //Capped at 100 seconds.
        }

        [Theory]
        [InlineData("Cache-Control: no-store\r\n")]
        [InlineData("Cache-Control: private, max-age=60\r\n")]
        [InlineData("Cache-Control: no-cache\r\n")]
        [InlineData("Cache-Control: max-age=0\r\n")]
        [InlineData("Pragma: no-cache\r\n")]
        [InlineData("Set-Cookie: session=abc\r\n")]
        [InlineData("Vary: Accept-Language\r\n")]
        [InlineData("Vary: *\r\n")]
        public void Responses_that_may_be_about_one_client_are_not_kept(string header)
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), answer: Response(extraHeaders: header));

            Assert.False(Get(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void Vary_on_the_encoding_is_fine_and_each_encoding_has_its_own_copy()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), headers: "Accept-Encoding: gzip\r\n", answer: Response("GZIPPED-BYTES!!", "Vary: Accept-Encoding\r\nContent-Encoding: gzip\r\n"));

            Assert.Equal("GZIPPED-BYTES!!", Body(Get(plugin, new FakeConnection(), headers: "Accept-Encoding: gzip\r\n")));
            Assert.False(Get(plugin, new FakeConnection(), headers: "Accept-Encoding: br\r\n").IsDropped);
            Assert.False(Get(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void The_query_is_part_of_the_page_and_the_host_too()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), "/p?id=1", answer: Response("one"));

            Assert.True(Get(plugin, new FakeConnection(), "/p?id=1").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/p?id=2").IsDropped);

            var other = new FakeConnection();
            Assert.False(Send(plugin, other, Http.Request("/p?id=1", host: "other.example.com")).IsDropped);
        }

        [Fact]
        public void Requests_about_one_client_are_not_answered_from_the_cache_and_do_not_fill_it()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), answer: Response());

            Assert.False(Get(plugin, new FakeConnection(), headers: "Cookie: session=1\r\n").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), headers: "Authorization: Bearer x\r\n").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), headers: "Range: bytes=0-10\r\n").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), headers: "If-None-Match: \"abc\"\r\n").IsDropped);
            Assert.False(Send(plugin, new FakeConnection(), Http.Request("/page", "POST", body: "x=1")).IsDropped);

            Get(plugin, new FakeConnection(), "/private", headers: "Cookie: s=1\r\n", answer: Response("secret"));
            Assert.False(Get(plugin, new FakeConnection(), "/private").IsDropped);
        }

        [Fact]
        public void Cookies_can_be_allowed_for_sites_that_send_them_for_everything()
        {
            var (plugin, _) = Start(new() { ["AllowCookies"] = "true" });
            Get(plugin, new FakeConnection(), headers: "Cookie: _ga=1\r\n", answer: Response());

            Assert.True(Get(plugin, new FakeConnection(), headers: "Cookie: _ga=2\r\n").IsDropped);
        }

        [Fact]
        public void A_reload_is_answered_from_the_cache_unless_the_setting_says_to_honor_it()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), answer: Response());
            Assert.True(Get(plugin, new FakeConnection(), headers: "Cache-Control: no-cache\r\n").IsDropped);

            var (strict, _) = Start(new() { ["HonorRequestCacheControl"] = "true" });
            Get(strict, new FakeConnection(), answer: Response());
            Assert.False(Get(strict, new FakeConnection(), headers: "Cache-Control: max-age=0\r\n").IsDropped);
            Assert.True(Get(strict, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void Only_the_hosts_and_paths_that_are_asked_for_are_kept()
        {
            var (plugin, _) = Start(new() { ["Hosts"] = "*.example.com", ["Paths"] = "/static/, /img/", ["ExcludePaths"] = "/static/admin" });

            Get(plugin, new FakeConnection(), "/static/app.js", answer: Response("js"));
            Get(plugin, new FakeConnection(), "/other", answer: Response("page"));
            Get(plugin, new FakeConnection(), "/static/admin/x.js", answer: Response("admin"));
            var elsewhere = new FakeConnection();
            Send(plugin, elsewhere, Http.Request("/static/app.js", host: "www.other.org"));
            Send(plugin, elsewhere, Response("js"), direction: NpDataDirection.EndpointToClient);

            Assert.True(Get(plugin, new FakeConnection(), "/static/app.js").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/other").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/static/admin/x.js").IsDropped);
            Assert.False(Send(plugin, new FakeConnection(), Http.Request("/static/app.js", host: "www.other.org")).IsDropped);
        }

        [Fact]
        public void Statuses_and_sizes_are_limited()
        {
            var (plugin, _) = Start(new() { ["MaxObjectKB"] = "1" });

            Get(plugin, new FakeConnection(), "/missing", answer: Response("nope", status: 404));
            Get(plugin, new FakeConnection(), "/big", answer: Response(new string('x', 2000)));
            Get(plugin, new FakeConnection(), "/small", answer: Response(new string('x', 500)));

            Assert.False(Get(plugin, new FakeConnection(), "/missing").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/big").IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), "/small").IsDropped);

            var (statuses, _) = Start(new() { ["Statuses"] = "200, 404" });
            Get(statuses, new FakeConnection(), "/missing", answer: Response("nope", status: 404));
            Assert.True(Get(statuses, new FakeConnection(), "/missing").IsDropped);
        }

        [Fact]
        public void A_response_that_comes_in_pieces_or_without_a_length_is_handled()
        {
            var (plugin, _) = Start();

            var pieces = new FakeConnection();
            Send(plugin, pieces, Http.Request("/pieces"));
            var answer = Response("0123456789");
            Send(plugin, pieces, answer.AsSpan(0, 20).ToArray(), direction: NpDataDirection.EndpointToClient);
            Send(plugin, pieces, answer.AsSpan(20, answer.Length - 25).ToArray(), direction: NpDataDirection.EndpointToClient);
            Send(plugin, pieces, answer.AsSpan(answer.Length - 5).ToArray(), direction: NpDataDirection.EndpointToClient);
            Assert.Equal("0123456789", Body(Get(plugin, new FakeConnection(), "/pieces")));

            Get(plugin, new FakeConnection(), "/chunked", answer: Http.Bytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n"));
            Assert.False(Get(plugin, new FakeConnection(), "/chunked").IsDropped);
        }

        [Fact]
        public void A_keep_alive_connection_gets_hits_between_requests_that_go_to_the_endpoint()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Get(plugin, connection, "/a", answer: Response("A"));
            Get(plugin, connection, "/b", headers: "Cookie: x=1\r\n", answer: Response("B")); //Not kept, but its answer is followed.
            Assert.True(Get(plugin, connection, "/a").IsDropped);
            Assert.False(Get(plugin, connection, "/c", answer: Response("C")).IsDropped);
            Assert.True(Get(plugin, connection, "/a").IsDropped);
        }

        [Fact]
        public void A_request_is_not_answered_from_the_cache_while_an_earlier_one_has_no_answer_yet()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), "/a", answer: Response("A"));

            var connection = new FakeConnection();
            Assert.False(Send(plugin, connection, Http.Request("/slow")).IsDropped); //Not answered yet.
            Assert.False(Send(plugin, connection, Http.Request("/a")).IsDropped); //Would be answered first, out of order.

            //Two in one read are not answered from the cache either.
            var pipelined = new FakeConnection();
            Assert.False(Send(plugin, pipelined, Http.Request("/a").Concat(Http.Request("/a")).ToArray()).IsDropped);
        }

        [Fact]
        public void The_answer_to_a_head_request_does_not_confuse_the_connection()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/a", "HEAD"));
            Send(plugin, connection, Http.Bytes("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\n\r\n"), direction: NpDataDirection.EndpointToClient);

            Get(plugin, connection, "/b", answer: Response("B"));
            Assert.True(Get(plugin, connection, "/b").IsDropped);
        }

        [Fact]
        public void A_head_that_arrives_in_pieces_is_held_back_and_a_hit_is_answered_whole()
        {
            var (plugin, _) = Start();
            Get(plugin, new FakeConnection(), answer: Response());

            var connection = new FakeConnection();
            var bytes = Http.Request("/page");
            var first = Send(plugin, connection, bytes.AsSpan(0, 10).ToArray());
            Assert.True(first.IsDropped);
            Assert.Null(first.Response);

            var second = Send(plugin, connection, bytes.AsSpan(10).ToArray());
            Assert.Contains("X-Cache: HIT", Split(second.Response).Head);
        }

        [Fact]
        public void Entries_that_do_not_fit_make_the_oldest_go()
        {
            var cache = new ResponseCache(maxBytes: 5000, maxEntries: 100, Clock);
            CachedResponse Entry(int size) => new()
            {
                Head = new NetProxy.Plugins.Common.HttpResponseHead { Status = 200 },
                Body = new byte[size],
                StoredUtc = Clock.GetUtcNow().UtcDateTime,
                ExpiresUtc = Clock.GetUtcNow().UtcDateTime.AddMinutes(1)
            };

            cache.Put("a", Entry(1000));
            cache.Put("b", Entry(1000));
            cache.Put("c", Entry(1000));
            Assert.NotNull(cache.Get("a")); //Asked for, so it is the newest now.
            cache.Put("d", Entry(1000));

            Assert.Null(cache.Get("b"));
            Assert.NotNull(cache.Get("a"));
            Assert.Equal(1, cache.Evicted);

            cache.Put("huge", Entry(100_000));
            Assert.Null(cache.Get("huge"));
            Assert.True(cache.Bytes <= 5000);

            Clock.Advance(TimeSpan.FromMinutes(2));
            Assert.Equal(3, cache.Purge());
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void Http2_tcp_and_the_answers_of_other_directions_are_left_alone()
        {
            var (plugin, host) = Start();

            Assert.False(Send(plugin, new FakeConnection { IsHttp2 = true }, Http.Request("/a"), canRespond: false).IsDropped);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));
            Assert.False(Send(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) }, Http.Request("/a")).IsDropped);
        }

        [Fact]
        public void Traffic_that_is_not_http_goes_through_untouched()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            byte[] binary = [0x16, 0x03, 0x01, 0x02, 0x00, 0x01, 0x00];

            var data = Send(plugin, connection, binary);
            Assert.False(data.IsDropped);
            Assert.Equal(binary, data.Data.ToArray());
            Assert.False(Get(plugin, connection, answer: Response()).IsDropped);
            Assert.False(Get(plugin, connection).IsDropped);
        }
    }
}
