using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using NetProxy.Plugins.SecurityHeaders;
using NetProxy.Plugins.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class SecurityHeadersTests : PluginTestBase
    {
        private (SecurityHeadersPlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
            => Start(new SecurityHeadersPlugin(), settings);

        private static byte[] Response(string body = "<html></html>", string headers = "", int status = 200)
            => Http.Bytes($"HTTP/1.1 {status} X\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\n{headers}\r\n{body}");

        private static string Changed(NpDataEventArgs data) => Encoding.Latin1.GetString(data.Data.Span);

        /// <summary>A request and its response, the way they pass through the proxy.</summary>
        private static NpDataEventArgs Exchange(SecurityHeadersPlugin plugin, FakeConnection connection, byte[] response, string method = "GET")
        {
            Send(plugin, connection, Http.Request("/", method));
            return Sending(plugin, connection, response);
        }

        private static List<string> HeaderLines(string text) => text[..text.IndexOf("\r\n\r\n", StringComparison.Ordinal)].Split("\r\n").Skip(1).ToList();

        [Fact]
        public void The_defaults_add_two_headers_and_take_out_the_ones_that_give_the_server_away()
        {
            var (plugin, _) = Start();

            var data = Exchange(plugin, new FakeConnection(), Response(headers: "Server: Kestrel\r\nX-Powered-By: ASP.NET\r\n"));

            Assert.True(data.IsModified);
            var lines = HeaderLines(Changed(data));
            Assert.Contains("X-Content-Type-Options: nosniff", lines);
            Assert.Contains("Referrer-Policy: strict-origin-when-cross-origin", lines);
            Assert.DoesNotContain(lines, o => o.StartsWith("Server:") || o.StartsWith("X-Powered-By:"));
            Assert.Contains("Content-Length: 13", lines);
            Assert.EndsWith("\r\n\r\n<html></html>", Changed(data));
        }

        [Fact]
        public void Hsts_is_only_added_to_https_and_only_when_asked_for()
        {
            var (off, _) = Start();
            Assert.DoesNotContain(HeaderLines(Changed(Exchange(off, new FakeConnection(), Response()))), o => o.StartsWith("Strict-Transport-Security"));

            var (plugin, _) = Start(new() { ["HstsSeconds"] = "31536000", ["HstsIncludeSubDomains"] = "true", ["HstsPreload"] = "true" });

            Assert.Contains("Strict-Transport-Security: max-age=31536000; includeSubDomains; preload", HeaderLines(Changed(Exchange(plugin, new FakeConnection(), Response()))));
            Assert.DoesNotContain(HeaderLines(Changed(Exchange(plugin, new FakeConnection { ClientTls = null }, Response()))), o => o.StartsWith("Strict-Transport-Security"));
        }

        [Fact]
        public void The_other_security_headers_are_added_when_they_are_set()
        {
            var (plugin, _) = Start(new()
            {
                ["FrameOptions"] = "DENY", ["ContentSecurityPolicy"] = "default-src 'self'", ["PermissionsPolicy"] = "camera=()", ["CrossOriginOpenerPolicy"] = "same-origin",
                ["ReferrerPolicy"] = "no-referrer", ["ContentTypeOptions"] = "false"
            });

            var lines = HeaderLines(Changed(Exchange(plugin, new FakeConnection(), Response())));

            Assert.Contains("X-Frame-Options: DENY", lines);
            Assert.Contains("Content-Security-Policy: default-src 'self'", lines);
            Assert.Contains("Permissions-Policy: camera=()", lines);
            Assert.Contains("Cross-Origin-Opener-Policy: same-origin", lines);
            Assert.Contains("Referrer-Policy: no-referrer", lines);
            Assert.DoesNotContain(lines, o => o.StartsWith("X-Content-Type-Options"));
        }

        [Fact]
        public void A_header_the_site_sends_is_left_alone_unless_told_otherwise()
        {
            var (plugin, _) = Start(new() { ["FrameOptions"] = "DENY" });
            var lines = HeaderLines(Changed(Exchange(plugin, new FakeConnection(), Response(headers: "X-Frame-Options: ALLOWALL\r\nReferrer-Policy: origin\r\n"))));
            Assert.Contains("X-Frame-Options: ALLOWALL", lines);
            Assert.Contains("Referrer-Policy: origin", lines);

            var (forcing, _) = Start(new() { ["FrameOptions"] = "DENY", ["OnlyIfMissing"] = "false" });
            lines = HeaderLines(Changed(Exchange(forcing, new FakeConnection(), Response(headers: "X-Frame-Options: ALLOWALL\r\n"))));
            Assert.Equal(["X-Frame-Options: DENY"], lines.Where(o => o.StartsWith("X-Frame")));
        }

        [Fact]
        public void Headers_of_your_own_can_be_added_and_the_removed_ones_chosen()
        {
            var (plugin, host) = Start(new() { ["Header1"] = "X-Served-By: edge-1", ["Header2"] = "nocolon", ["Header3"] = "Bad Name: x", ["RemoveHeaders"] = "Via, X-Debug" });

            var lines = HeaderLines(Changed(Exchange(plugin, new FakeConnection(), Response(headers: "Via: 1.1 x\r\nX-Debug: 1\r\nServer: Kestrel\r\n"))));

            Assert.Contains("X-Served-By: edge-1", lines);
            Assert.Contains("Server: Kestrel", lines); //Not in the list that was given.
            Assert.DoesNotContain(lines, o => o.StartsWith("Via:") || o.StartsWith("X-Debug:"));
            Assert.Contains(host.Texts, o => o.StartsWith("Header2 is left out"));
            Assert.Contains(host.Texts, o => o.Contains("'Bad Name'"));
        }

        [Fact]
        public void A_header_that_is_sent_twice_keeps_both_and_their_order()
        {
            var (plugin, _) = Start();

            var lines = HeaderLines(Changed(Exchange(plugin, new FakeConnection(), Response(headers: "Set-Cookie: a=1\r\nSet-Cookie: b=2\r\n"))));

            Assert.Equal(["Set-Cookie: a=1", "Set-Cookie: b=2"], lines.Where(o => o.StartsWith("Set-Cookie")));
            Assert.True(lines.IndexOf("Set-Cookie: a=1") < lines.IndexOf("Set-Cookie: b=2"));
        }

        [Fact]
        public void Every_response_of_a_keep_alive_connection_is_changed_and_a_body_that_looks_like_a_response_is_not()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            var trap = "HTTP/1.1 200 OK\r\nServer: fake\r\n\r\n";

            Send(plugin, connection, Http.Request("/a").Concat(Http.Request("/b")).ToArray());
            var data = Sending(plugin, connection, Response(trap).Concat(Response("second")).ToArray());

            var text = Changed(data);
            Assert.Equal(2, text.Split("X-Content-Type-Options: nosniff").Length - 1);
            Assert.Contains(trap + "HTTP/1.1 200 X", text); //The body is as it was, and so is the head after it.
            Assert.Contains("Server: fake", text);
        }

        [Fact]
        public void Responses_that_come_in_pieces_are_changed_when_their_head_starts_in_the_piece()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            var whole = Response("0123456789");

            Send(plugin, connection, Http.Request("/a"));
            var first = Sending(plugin, connection, whole.AsSpan(0, 15).ToArray());
            Assert.False(first.IsModified); //The head is not whole, and what there is of it has gone on.
            Assert.Equal(whole.AsSpan(0, 15).ToArray(), first.Data.ToArray());

            Assert.False(Sending(plugin, connection, whole.AsSpan(15).ToArray()).IsModified);

            //The next response on the connection is changed, though the one before it was not.
            Send(plugin, connection, Http.Request("/b"));
            Assert.True(Sending(plugin, connection, Response("next")).IsModified);
        }

        [Fact]
        public void A_response_that_starts_in_the_middle_of_a_read_is_changed_where_it_is()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/a").Concat(Http.Request("/b")).ToArray());
            var first = Response("one");
            var second = Response("two");
            var data = Sending(plugin, connection, first.Concat(second.AsSpan(0, second.Length - 3).ToArray()).ToArray());
            Sending(plugin, connection, second.AsSpan(second.Length - 3).ToArray());

            Assert.Equal(2, Changed(data).Split("nosniff").Length - 1);
            Assert.EndsWith("\r\n\r\n", Changed(data)[..(Changed(data).LastIndexOf("\r\n\r\n") + 4)]);
        }

        [Fact]
        public void A_response_to_head_a_response_without_a_body_and_an_interim_response_do_not_throw_off_the_next()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            var head = Exchange(plugin, connection, Http.Bytes("HTTP/1.1 200 OK\r\nContent-Length: 5000\r\n\r\n"), "HEAD");
            Assert.True(head.IsModified);

            Send(plugin, connection, Http.Request("/b"));
            var interim = Sending(plugin, connection, Http.Bytes("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 304 Not Modified\r\nServer: x\r\n\r\n"));
            Assert.Equal(1, Changed(interim).Split("nosniff").Length - 1); //The 100 is not changed, the 304 is.
            Assert.StartsWith("HTTP/1.1 100 Continue\r\n\r\n", Changed(interim));

            Assert.True(Exchange(plugin, connection, Response("after")).IsModified);
        }

        [Fact]
        public void An_upgrade_http2_tcp_and_the_requests_of_the_client_are_left_alone()
        {
            var (plugin, host) = Start();

            var upgrade = Exchange(plugin, new FakeConnection(), Http.Bytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nServer: x\r\n\r\n"));
            Assert.False(upgrade.IsModified);

            Assert.False(Sending(plugin, new FakeConnection { IsHttp2 = true }, Response()).IsModified);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));

            Assert.False(Sending(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) }, Response()).IsModified);
            Assert.False(Send(plugin, new FakeConnection(), Http.Request("/")).IsModified);
        }

        [Fact]
        public void Traffic_that_is_not_http_goes_through_untouched()
        {
            var (plugin, _) = Start();
            byte[] binary = [0x16, 0x03, 0x01, 0x02, 0x00, 0x01, 0x00];

            var data = Sending(plugin, new FakeConnection(), binary);

            Assert.False(data.IsModified);
            Assert.Equal(binary, data.Data.ToArray());
        }

        [Fact]
        public void The_response_head_can_be_read_and_written_as_it_was()
        {
            var bytes = Http.Bytes("HTTP/1.1 301 Moved\r\nLocation: /x\r\nSet-Cookie: a=1\r\nSet-Cookie: b=2\r\nContent-Length: 0\r\n\r\n");
            var head = HttpResponseHead.TryParse(bytes.AsSpan(0, bytes.Length - 4))!;

            Assert.Equal(301, head.Status);
            Assert.Equal("Moved", head.Reason);
            Assert.Equal(["a=1", "b=2"], head.GetAll("set-cookie"));
            Assert.Equal(bytes, head.ToBytes());
            Assert.Null(HttpResponseHead.TryParse("GET / HTTP/1.1\r\nHost: x"u8));
            Assert.Null(HttpResponseHead.TryParse("HTTP/1.1 200 OK\r\n folded"u8));
        }
    }
}
