using NetProxy.Plugins.Redirect;
using NetProxy.Plugins.Tests.Support;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class RedirectRuleTests
    {
        private static string? Apply(string rule, string url)
        {
            var parsed = RedirectRule.Parse(rule, out var error);
            Assert.True(parsed != null, error);

            var uri = new Uri(url);
            return parsed.Apply(new RequestedUrl(uri.Scheme, uri.IdnHost, uri.Port, uri.AbsolutePath, uri.Query));
        }

        [Fact]
        public void The_example_that_sends_http_to_https_for_one_page()
            => Assert.Equal("https://ntdls.com/page.html", Apply("http://ntdls.com/page.html -> https://ntdls.com/page.html", "http://ntdls.com/page.html"));

        [Fact]
        public void The_example_that_sends_a_page_to_the_front_page_of_a_site()
            => Assert.Equal("http://example.com", Apply("http://example.com/page.html -> http://example.com", "http://example.com/page.html"));

        [Fact]
        public void A_rule_is_only_for_the_urls_it_matches()
        {
            Assert.Null(Apply("http://ntdls.com/page.html -> https://ntdls.com/page.html", "https://ntdls.com/page.html"));
            Assert.Null(Apply("http://ntdls.com/page.html -> https://ntdls.com/page.html", "http://ntdls.com/other.html"));
            Assert.Null(Apply("http://ntdls.com/page.html -> https://ntdls.com/page.html", "http://www.ntdls.com/page.html"));
            Assert.Null(Apply("http://ntdls.com/page.html -> https://ntdls.com/page.html", "http://ntdls.com/page.html/"));
        }

        [Fact]
        public void Host_and_path_are_not_case_sensitive()
            => Assert.Equal("https://x.com/new", Apply("HTTP://NTDLS.com/Old -> https://x.com/new", "http://ntdls.COM/old"));

        [Fact]
        public void Everything_on_http_can_go_to_https_with_its_path_and_query()
        {
            const string rule = "http://*/* -> https://{host}{url}";

            Assert.Equal("https://ntdls.com/a/b?x=1", Apply(rule, "http://ntdls.com/a/b?x=1"));
            Assert.Equal("https://www.example.org/", Apply(rule, "http://www.example.org/"));
            Assert.Null(Apply(rule, "https://ntdls.com/a"));
        }

        [Fact]
        public void The_parts_that_a_star_caught_can_be_used()
        {
            Assert.Equal("https://www.example.com/new/a/b.html", Apply("http://*.example.com/old/* -> https://www.example.com/new/{2}", "http://shop.example.com/old/a/b.html"));
            Assert.Equal("https://shop.example.com/", Apply("*.example.com -> https://{1}.example.com/", "http://shop.example.com/anything"));
        }

        [Fact]
        public void A_query_is_sent_on_only_when_asked_for()
        {
            Assert.Equal("https://a.com/p", Apply("http://a.com/p -> https://a.com/p", "http://a.com/p?x=1"));
            Assert.Equal("https://a.com/p?x=1", Apply("http://a.com/p -> https://a.com/p keepquery", "http://a.com/p?x=1"));
            Assert.Equal("https://a.com/p?x=1", Apply("http://a.com/p -> https://a.com/p{query}", "http://a.com/p?x=1"));
            Assert.Equal("https://a.com/p?fixed=1", Apply("http://a.com/p -> https://a.com/p?fixed=1 keepquery", "http://a.com/p?x=1"));
        }

        [Fact]
        public void A_port_in_the_match_is_matched_and_a_port_that_is_not_usual_is_in_hostport()
        {
            Assert.Equal("https://a.com:8443/", Apply("http://a.com:8080/* -> https://a.com:8443/", "http://a.com:8080/x"));
            Assert.Null(Apply("http://a.com:8080/* -> https://a.com:8443/", "http://a.com/x"));
            Assert.Equal("https://a.com:8080/x", Apply("http://a.com:*/* -> https://{hostport}{path}", "http://a.com:8080/x"));
        }

        [Fact]
        public void A_rule_that_would_send_a_client_to_where_it_already_is_does_not_apply()
        {
            Assert.Null(Apply("http://a.com/* -> http://a.com", "http://a.com/"));
            Assert.Null(Apply("*://a.com/* -> https://a.com{url}", "https://a.com/x"));
            Assert.Null(Apply("a.com/* -> /x", "http://a.com/x"));
            Assert.Equal("/x", Apply("a.com/* -> /x", "http://a.com/y"));
        }

        [Theory]
        [InlineData("nonsense")]
        [InlineData("a.com -> ftp://b.com")]
        [InlineData("a.com => https://b.com")]
        [InlineData("a.com -> https://b.com sometimes")]
        [InlineData("http:// -> https://b.com")]
        public void Rules_that_are_wrong_say_so(string text)
        {
            Assert.Null(RedirectRule.Parse(text, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void The_status_of_a_rule_can_be_set()
        {
            var rule = RedirectRule.Parse("a.com -> https://b.com 302", out _);
            Assert.Equal(302, rule!.Status);
            Assert.Equal(0, RedirectRule.Parse("a.com -> https://b.com", out _)!.Status);
        }
    }

    public class RedirectPluginTests : PluginTestBase
    {
        private (RedirectPlugin Plugin, FakeHost Host) Start(params string[] rules)
        {
            var settings = new Dictionary<string, string>();
            for (var i = 0; i < rules.Length; i++)
            {
                settings[$"Rule{i + 1}"] = rules[i];
            }

            return Start(new RedirectPlugin(), settings);
        }

        private static FakeConnection Plain() => new() { Proxy = new(Guid.NewGuid(), "web", 80, NetProxy.PluginApi.NpTrafficKind.Http), ClientTls = null };

        [Fact]
        public void A_request_that_a_rule_fits_is_answered_and_never_reaches_the_endpoint()
        {
            var (plugin, _) = Start("http://ntdls.com/page.html -> https://ntdls.com/page.html");

            var data = Send(plugin, Plain(), Http.Request("/page.html", host: "ntdls.com"));

            Assert.True(data.IsDropped);
            Assert.False(data.CloseAfterResponse);
            var (head, body) = Split(data.Response);
            Assert.StartsWith("HTTP/1.1 301 Moved Permanently\r\n", head);
            Assert.Contains("Location: https://ntdls.com/page.html\r\n", head);
            Assert.Contains("Content-Length: 0", head);
            Assert.Contains("Connection: keep-alive", head);
            Assert.Equal(string.Empty, body);
        }

        [Fact]
        public void The_example_to_a_front_page_and_a_request_no_rule_fits()
        {
            var (plugin, _) = Start("http://ntdls.com/page.html -> https://ntdls.com/page.html", "http://example.com/page.html -> http://example.com");

            var data = Send(plugin, Plain(), Http.Request("/page.html", host: "example.com"));
            Assert.Contains("Location: http://example.com\r\n", Split(data.Response).Head);

            Assert.False(Send(plugin, Plain(), Http.Request("/other.html", host: "example.com")).IsDropped);
            Assert.False(Send(plugin, Plain(), Http.Request("/page.html", host: "elsewhere.com")).IsDropped);
        }

        [Fact]
        public void The_scheme_comes_from_the_connection_and_the_port_from_the_host_header()
        {
            var (plugin, _) = Start("http://*/* -> https://{hostport}{url} 308");

            var tls = new FakeConnection(); //Has a client TLS version, so it is https.
            Assert.False(Send(plugin, tls, Http.Request("/a", host: "ntdls.com")).IsDropped);

            var data = Send(plugin, Plain(), Http.Request("/a?b=1", host: "ntdls.com:8080"));
            var (head, _) = Split(data.Response);
            Assert.StartsWith("HTTP/1.1 308 Permanent Redirect", head);
            Assert.Contains("Location: https://ntdls.com:8080/a?b=1\r\n", head);
        }

        [Fact]
        public void The_first_rule_that_fits_wins_and_the_status_has_a_default()
        {
            var settings = new Dictionary<string, string>
            {
                ["Status"] = "302",
                ["Rule2"] = "a.com -> https://two.com",
                ["Rule10"] = "a.com -> https://ten.com",
                ["Rule1"] = "a.com/x -> https://one.com"
            };
            var (plugin, _) = Start(new RedirectPlugin(), settings);

            Assert.Contains("302 Found", Split(Send(plugin, Plain(), Http.Request("/x", host: "a.com")).Response).Head);
            Assert.Contains("Location: https://one.com\r\n", Split(Send(plugin, Plain(), Http.Request("/x", host: "a.com")).Response).Head);
            Assert.Contains("Location: https://two.com\r\n", Split(Send(plugin, Plain(), Http.Request("/y", host: "a.com")).Response).Head);
        }

        [Fact]
        public void A_request_that_is_not_the_only_one_or_has_a_body_closes_the_connection()
        {
            var (plugin, _) = Start("a.com -> https://b.com");

            var two = Send(plugin, Plain(), Http.Request("/1", host: "a.com").Concat(Http.Request("/2", host: "a.com")).ToArray());
            Assert.True(two.CloseAfterResponse);
            Assert.Contains("Connection: close", Split(two.Response).Head);

            var post = Http.Request("/form", "POST", "a.com", body: "0123456789");
            Assert.True(Send(plugin, Plain(), post.AsSpan(0, post.Length - 4).ToArray()).CloseAfterResponse);
        }

        [Fact]
        public void A_head_that_arrives_in_pieces_is_held_back_and_then_redirected_or_passed_on_whole()
        {
            var (plugin, _) = Start("a.com/old -> https://b.com/new");
            var connection = Plain();
            var bytes = Http.Request("/old", host: "a.com");

            var first = Send(plugin, connection, bytes.AsSpan(0, 12).ToArray());
            Assert.True(first.IsDropped); //Held back: nothing of a half request goes to the endpoint.
            Assert.Null(first.Response);

            var second = Send(plugin, connection, bytes.AsSpan(12).ToArray());
            Assert.Contains("Location: https://b.com/new", Split(second.Response).Head);

            //A request that no rule fits is passed on whole, with the part that was held back.
            var other = Http.Request("/fine", host: "a.com");
            var connection2 = Plain();
            Assert.True(Send(plugin, connection2, other.AsSpan(0, 9).ToArray()).IsDropped);
            var rest = Send(plugin, connection2, other.AsSpan(9).ToArray());
            Assert.False(rest.IsDropped);
            Assert.Equal(other, rest.Data.ToArray());
        }

        [Fact]
        public void A_request_that_is_answered_while_others_wait_is_handled_in_order()
        {
            var (plugin, _) = Start("a.com/old -> https://b.com/new");
            var connection = Plain();

            Assert.False(Send(plugin, connection, Http.Request("/fine", host: "a.com")).IsDropped);
            Assert.True(Send(plugin, connection, Http.Request("/old", host: "a.com")).IsDropped);
            Assert.False(Send(plugin, connection, Http.Request("/fine-again", host: "a.com")).IsDropped);
        }

        [Fact]
        public void A_body_is_not_taken_for_a_request()
        {
            var (plugin, _) = Start("a.com/old -> https://b.com/new");
            var connection = Plain();
            var post = Http.Request("/form", "POST", "a.com", body: "GET /old HTTP/1.1\r\nHost: a.com\r\n\r\n");

            Assert.False(Send(plugin, connection, post).IsDropped);
        }

        [Fact]
        public void Http2_tcp_and_the_answers_of_the_endpoint_are_left_alone()
        {
            var (plugin, host) = Start("a.com -> https://b.com");

            Assert.False(Send(plugin, new FakeConnection { IsHttp2 = true, ClientTls = null }, Http.Request("/", host: "a.com"), canRespond: false).IsDropped);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));

            var fromEndpoint = Send(plugin, Plain(), Http.Request("/", host: "a.com"), direction: NetProxy.PluginApi.NpDataDirection.EndpointToClient);
            Assert.False(fromEndpoint.IsDropped);

            var udp = new FakeConnection { Proxy = new(Guid.NewGuid(), "u", 53, NetProxy.PluginApi.NpTrafficKind.Udp) };
            Assert.False(Send(plugin, udp, Http.Request("/", host: "a.com")).IsDropped);
        }

        [Fact]
        public void Traffic_that_is_not_http_goes_through_untouched()
        {
            var (plugin, _) = Start("a.com -> https://b.com");
            var connection = Plain();
            byte[] binary = [0x16, 0x03, 0x01, 0x02, 0x00, 0x01, 0x00];

            var data = Send(plugin, connection, binary);
            Assert.False(data.IsDropped);
            Assert.Equal(binary, data.Data.ToArray());
            Assert.False(Send(plugin, connection, Http.Request("/", host: "a.com")).IsDropped); //Not followed any more.
        }

        [Fact]
        public void Rules_that_are_wrong_are_reported_and_left_out()
        {
            var (plugin, host) = Start("nonsense", "a.com -> https://b.com");

            Assert.Single(host.Texts, o => o.StartsWith("Rule1 is left out."));
            Assert.Contains(host.Texts, o => o.StartsWith("1 redirect rule(s)"));
            Assert.True(Send(plugin, Plain(), Http.Request("/", host: "a.com")).IsDropped);
        }

        //---------------------------------------------------------------- ACME

        [Fact]
        public void An_acme_challenge_is_answered_from_the_folder_before_the_rules()
        {
            var directory = TempDirectory();
            File.WriteAllText(Path.Combine(directory, "token-1_A"), "token-1_A.thumbprint");
            var (plugin, _) = Start(new RedirectPlugin(), new() { ["AcmeDirectory"] = directory, ["Rule1"] = "http://*/* -> https://{host}{url}" });

            var data = Send(plugin, Plain(), Http.Request("/.well-known/acme-challenge/token-1_A", host: "ntdls.com"));
            var (head, body) = Split(data.Response);

            Assert.StartsWith("HTTP/1.1 200 OK", head);
            Assert.Equal("token-1_A.thumbprint", body);

            //Everything else on the port is redirected, and a token that has no file falls through to the rules.
            Assert.Contains("Location: https://ntdls.com/index.html", Split(Send(plugin, Plain(), Http.Request("/index.html", host: "ntdls.com")).Response).Head);
            Assert.Contains("Location: https://ntdls.com/.well-known/acme-challenge/nope", Split(Send(plugin, Plain(), Http.Request("/.well-known/acme-challenge/nope", host: "ntdls.com")).Response).Head);
        }

        [Fact]
        public void A_challenge_can_not_reach_outside_the_folder()
        {
            var directory = TempDirectory();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(directory)!, "secret.txt"), "secret");
            var (plugin, _) = Start(new RedirectPlugin(), new() { ["AcmeDirectory"] = directory });

            try
            {
                Assert.False(Send(plugin, Plain(), Http.Request("/.well-known/acme-challenge/../secret.txt")).IsDropped);
                Assert.False(Send(plugin, Plain(), Http.Request("/.well-known/acme-challenge/..%2Fsecret.txt")).IsDropped);
                Assert.False(Send(plugin, Plain(), Http.Request("/.well-known/acme-challenge/a/b")).IsDropped);
            }
            finally
            {
                File.Delete(Path.Combine(Path.GetDirectoryName(directory)!, "secret.txt"));
            }
        }
    }
}
