using NetProxy.Plugins.AntiHammer;
using NetProxy.Plugins.Common;
using NetProxy.Plugins.Tests.Support;
using System.Net;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class AddressRangeTests
    {
        [Theory]
        [InlineData("10.1.2.3", "10.0.0.0/8", true)]
        [InlineData("11.1.2.3", "10.0.0.0/8", false)]
        [InlineData("192.168.1.9", "192.168.1.9", true)]
        [InlineData("192.168.1.10", "192.168.1.9", false)]
        [InlineData("172.31.255.255", "172.16.0.0/12", true)]
        [InlineData("172.32.0.1", "172.16.0.0/12", false)]
        [InlineData("2606:4700::1", "2606:4700::/32", true)]
        [InlineData("2606:4701::1", "2606:4700::/32", false)]
        [InlineData("::ffff:10.1.2.3", "10.0.0.0/8", true)]
        [InlineData("10.1.2.3", "2606:4700::/32", false)]
        public void A_range_contains_what_is_in_its_block(string address, string range, bool expected)
        {
            Assert.True(AddressRange.TryParse(range, out var parsed));
            Assert.Equal(expected, parsed!.Contains(IPAddress.Parse(address)));
        }

        [Theory]
        [InlineData("not an address")]
        [InlineData("10.0.0.0/40")]
        [InlineData("10.0.0.0/x")]
        [InlineData("")]
        public void Nonsense_is_not_a_range(string text) => Assert.False(AddressRange.TryParse(text, out _));

        [Fact]
        public void Cloudflare_and_private_ranges_know_their_own()
        {
            Assert.Contains(AddressRange.Cloudflare, o => o.Contains(IPAddress.Parse("104.16.1.1")));
            Assert.Contains(AddressRange.Cloudflare, o => o.Contains(IPAddress.Parse("172.67.10.20")));
            Assert.Contains(AddressRange.Cloudflare, o => o.Contains(IPAddress.Parse("2606:4700:10::1")));
            Assert.DoesNotContain(AddressRange.Cloudflare, o => o.Contains(IPAddress.Parse("8.8.8.8")));
            Assert.Contains(AddressRange.Private, o => o.Contains(IPAddress.Parse("192.168.0.5")));
            Assert.DoesNotContain(AddressRange.Private, o => o.Contains(IPAddress.Parse("8.8.8.8")));
        }

        [Fact]
        public void An_ipv6_client_is_counted_as_its_whole_64()
        {
            Assert.Equal(AddressRange.KeyOf(IPAddress.Parse("2001:db8:1:2::1")), AddressRange.KeyOf(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd")));
            Assert.NotEqual(AddressRange.KeyOf(IPAddress.Parse("2001:db8:1:2::1")), AddressRange.KeyOf(IPAddress.Parse("2001:db8:1:3::1")));
            Assert.Equal("1.2.3.4", AddressRange.KeyOf(IPAddress.Parse("::ffff:1.2.3.4")));
        }

        [Theory]
        [InlineData("1.2.3.4:443", "1.2.3.4")]
        [InlineData("[2001:db8::1]:443", "2001:db8::1")]
        [InlineData(" 1.2.3.4 ", "1.2.3.4")]
        public void An_address_is_read_with_or_without_a_port(string text, string expected)
        {
            Assert.True(AddressRange.TryParseAddress(text, out var address));
            Assert.Equal(IPAddress.Parse(expected), address);
        }
    }

    public class AntiHammerSettingsTests
    {
        [Fact]
        public void With_no_settings_it_only_logs()
        {
            var settings = AntiHammerSettings.From(new Dictionary<string, string>(), out var problems);

            Assert.Empty(problems);
            Assert.True(settings.LogOnly);
            Assert.Equal(60, settings.BanMinutes);
            Assert.True(settings.IsAllowed(IPAddress.Parse("192.168.1.1")));
            Assert.False(settings.IsAllowed(IPAddress.Parse("8.8.8.8")));
            Assert.False(settings.IsTrustedProxy(IPAddress.Parse("104.16.1.1")));
        }

        [Fact]
        public void Bad_values_are_reported_and_replaced_by_the_default()
        {
            var settings = AntiHammerSettings.From(new Dictionary<string, string>
            {
                ["BanMinutes"] = "soon", ["LogOnly"] = "maybe", ["MaxRequests"] = "1", ["Allow"] = "1.2.3.4, nonsense, 5.6.7.0/24"
            }, out var problems);

            Assert.Equal(4, problems.Count);
            Assert.Equal(60, settings.BanMinutes);
            Assert.True(settings.LogOnly);
            Assert.Equal(200, settings.MaxRequests);
            Assert.True(settings.IsAllowed(IPAddress.Parse("5.6.7.8")));
        }

        [Fact]
        public void Cloudflare_can_be_trusted_by_name_and_private_addresses_can_be_banned()
        {
            var settings = AntiHammerSettings.From(new Dictionary<string, string> { ["TrustedProxies"] = "cloudflare, 10.9.0.0/16", ["AllowPrivate"] = "false" }, out var problems);

            Assert.Empty(problems);
            Assert.True(settings.IsTrustedProxy(IPAddress.Parse("104.16.1.1")));
            Assert.True(settings.IsTrustedProxy(IPAddress.Parse("10.9.1.1")));
            Assert.False(settings.IsTrustedProxy(IPAddress.Parse("10.8.1.1")));
            Assert.False(settings.IsAllowed(IPAddress.Parse("192.168.1.1")));
        }

        [Fact]
        public void The_probe_paths_can_be_replaced_or_added_to()
        {
            var replaced = AntiHammerSettings.From(new Dictionary<string, string> { ["ProbePaths"] = "/secret, *.bak" }, out _);
            Assert.Equal("/secret", replaced.MatchProbe("/secret/x"));
            Assert.Equal(".bak", replaced.MatchProbe("/a/b.bak"));
            Assert.Null(replaced.MatchProbe("/.env"));

            var added = AntiHammerSettings.From(new Dictionary<string, string> { ["ExtraProbePaths"] = "/secret" }, out _);
            Assert.Equal("/secret", added.MatchProbe("/secret"));
            Assert.Equal("/.env", added.MatchProbe("/.env"));
            Assert.Null(added.MatchProbe("/index.html"));
            Assert.Null(added.MatchProbe("/products/shellfish"));
        }
    }

    public class RequestReaderTests
    {
        [Fact]
        public void A_request_is_found_with_the_headers_that_matter()
        {
            var seen = new RequestReader().Feed(Http.Request("/a/b?x=1", "GET", "Example.com", "X-Forwarded-For: 1.1.1.1\r\nX-Forwarded-For: 2.2.2.2\r\nCF-Connecting-IP: 3.3.3.3\r\n"));

            var request = Assert.Single(seen);
            Assert.Equal("GET", request.Method);
            Assert.Equal("/a/b?x=1", request.Target);
            Assert.Equal("Example.com", request.Host);
            Assert.Equal("1.1.1.1,2.2.2.2", request.ForwardedFor);
            Assert.Equal("3.3.3.3", request.ConnectingIp);
            Assert.False(request.BodyPending);
        }

        [Fact]
        public void A_head_that_arrives_in_pieces_is_found_when_it_is_whole()
        {
            var reader = new RequestReader();
            var bytes = Http.Request("/slow");

            Assert.Empty(reader.Feed(bytes.AsSpan(0, 10)));
            Assert.Empty(reader.Feed(bytes.AsSpan(10, 20)));
            Assert.Equal("/slow", Assert.Single(reader.Feed(bytes.AsSpan(30))).Target);
        }

        [Fact]
        public void Several_requests_in_one_read_are_all_found_and_bodies_are_skipped()
        {
            var seen = new RequestReader().Feed(Http.Request("/a").Concat(Http.Request("/post", "POST", body: "GET /inside HTTP/1.1\r\n\r\n")).Concat(Http.Request("/c")).ToArray());

            Assert.Equal(["/a", "/post", "/c"], seen.Select(o => o.Target));
        }

        [Fact]
        public void A_body_that_comes_in_a_later_read_is_skipped_too()
        {
            var reader = new RequestReader();
            var head = Http.Request("/upload", "POST", body: "0123456789");
            var cut = head.Length - 6;

            var first = Assert.Single(reader.Feed(head.AsSpan(0, cut)));
            Assert.True(first.BodyPending);

            Assert.Empty(reader.Feed(Http.Bytes("GET /x"))); //The last six bytes of the body.
            Assert.Equal("/next", Assert.Single(reader.Feed(Http.Request("/next"))).Target);
        }

        [Fact]
        public void What_is_not_http_makes_the_reader_stop_and_fault()
        {
            var binary = new RequestReader();
            Assert.Empty(binary.Feed([0x16, 0x03, 0x01, 0x02, 0x00]));
            Assert.True(binary.IsStopped);
            Assert.True(binary.IsFaulted);
            Assert.Empty(binary.Feed(Http.Request("/a")));
        }

        [Fact]
        public void A_chunked_body_is_followed_to_its_end_and_the_next_request_is_found()
        {
            var reader = new RequestReader();
            var chunked = Http.Request("/up", "POST", extraHeaders: "Transfer-Encoding: chunked\r\n");
            var body = Http.Bytes("5\r\nGET /\r\n3;ext=1\r\nabc\r\n0\r\nTrailer: x\r\n\r\n");

            var first = Assert.Single(reader.Feed(chunked.Concat(body.Take(7)).ToArray()));
            Assert.True(first.BodyPending);
            Assert.Empty(reader.Feed(body.Skip(7).ToArray()));
            Assert.False(reader.IsStopped);
            Assert.Equal("/next", Assert.Single(reader.Feed(Http.Request("/next"))).Target);
        }

        [Fact]
        public void An_upgraded_connection_stops_without_a_fault()
        {
            var reader = new RequestReader();

            var request = Assert.Single(reader.Feed(Http.Request("/ws", extraHeaders: "Upgrade: websocket\r\nConnection: Upgrade\r\n")));

            Assert.Equal("/ws", request.Target);
            Assert.True(reader.IsStopped);
            Assert.False(reader.IsFaulted);
        }

        [Fact]
        public void The_buffer_holds_back_a_head_that_has_not_arrived_whole()
        {
            var reader = new RequestReader(keepBuffer: true);
            var bytes = Http.Bytes(Http.Text(Http.Request("/a")) + Http.Text(Http.Request("/b")));
            var cut = bytes.Length - 10;

            var first = reader.Feed(bytes.AsSpan(0, cut));
            Assert.Equal("/a", Assert.Single(first).Target);
            Assert.Equal(Http.Request("/a").Length, reader.HeldFrom); //Only the first request is whole.
            Assert.Equal(cut, reader.Buffer.Length);

            var second = reader.Feed(bytes.AsSpan(cut));
            Assert.Equal("/b", Assert.Single(second).Target);
            Assert.Equal(reader.Buffer.Length, reader.HeldFrom);
            Assert.Equal(Http.Request("/b").Length - 0, reader.Buffer.Length); //The held part and the rest: exactly the second request.
        }

        [Fact]
        public void The_headers_the_target_and_the_query_are_available()
        {
            var request = Assert.Single(new RequestReader().Feed(Http.Request("/a/b?x=1&y=2", extraHeaders: "Cookie: a=1\r\nCookie: b=2\r\n")));

            Assert.Equal("a=1, b=2", request.Header("cookie"));
            Assert.Equal("/a/b", request.Path);
            Assert.Equal("?x=1&y=2", request.Query);
            Assert.Equal("/", new SeenRequest("GET", "http://example.com", null, null, null, false).Path);
        }
    }

    public class HammerGuardTests
    {
        private readonly FakeClock _clock = new();
        private static readonly IPAddress Client = IPAddress.Parse("203.0.113.7");

        private HammerGuard Guard(Dictionary<string, string>? settings = null)
            => new(AntiHammerSettings.From(settings ?? [], out _), _clock);

        [Fact]
        public void Too_many_requests_in_the_window_is_a_ban_and_slow_ones_are_not()
        {
            var guard = Guard(new() { ["MaxRequests"] = "5", ["RequestWindowSeconds"] = "10" });

            for (var i = 0; i < 5; i++)
            {
                Assert.Null(guard.CountRequest(Client, $"/p{i}"));
            }

            var ban = guard.CountRequest(Client, "/p6");
            Assert.NotNull(ban);
            Assert.Contains("more than 5 requests in 10 seconds", ban.Reason);
            Assert.NotNull(guard.FindBan(Client));

            var slow = Guard(new() { ["MaxRequests"] = "5", ["RequestWindowSeconds"] = "10" });
            for (var i = 0; i < 50; i++)
            {
                Assert.Null(slow.CountRequest(Client, $"/p{i}"));
                _clock.Advance(TimeSpan.FromSeconds(3));
            }
        }

        [Fact]
        public void The_same_page_again_and_again_is_a_ban_even_when_the_rate_is_low()
        {
            var guard = Guard(new() { ["MaxSamePath"] = "10", ["SamePathWindowSeconds"] = "60" });

            BanEntry? ban = null;
            for (var i = 0; i < 11 && ban == null; i++)
            {
                ban = guard.CountRequest(Client, "/Cloud?page=" + i);
                _clock.Advance(TimeSpan.FromSeconds(2));
            }

            Assert.NotNull(ban);
            Assert.Contains("/cloud", ban.Reason);

            Assert.Null(guard.CountRequest(IPAddress.Parse("203.0.113.8"), "/Cloud")); //Others are not affected.
        }

        [Fact]
        public void Probes_are_a_ban_at_the_third()
        {
            var guard = Guard();

            Assert.Null(guard.CountRequest(Client, "/.env"));
            Assert.Null(guard.CountRequest(Client, "/index.html")); //Does not count as a probe.
            Assert.Null(guard.CountRequest(Client, "/wp-login.php?x=1"));
            var ban = guard.CountRequest(Client, "/PHPMyAdmin/index.php");

            Assert.NotNull(ban);
            Assert.Contains("not there", ban.Reason);
        }

        [Fact]
        public void Probes_that_are_far_apart_are_not()
        {
            var guard = Guard(new() { ["ProbeWindowMinutes"] = "10" });

            for (var i = 0; i < 20; i++)
            {
                Assert.Null(guard.CountRequest(Client, "/.env"));
                _clock.Advance(TimeSpan.FromMinutes(6));
                Assert.Null(guard.CountRequest(Client, "/index.html"));
                _clock.Advance(TimeSpan.FromMinutes(6));
            }
        }

        [Fact]
        public void Too_many_connections_is_a_ban()
        {
            var guard = Guard(new() { ["MaxConnections"] = "3" });

            Assert.Null(guard.CountConnection(Client));
            Assert.Null(guard.CountConnection(Client));
            Assert.Null(guard.CountConnection(Client));
            Assert.NotNull(guard.CountConnection(Client));
        }

        [Fact]
        public void A_ban_ends_and_the_next_one_is_twice_as_long_up_to_the_maximum()
        {
            var guard = Guard(new() { ["MaxConnections"] = "2", ["BanMinutes"] = "10", ["MaxBanHours"] = "1" });
            var durations = new List<TimeSpan>();

            for (var round = 0; round < 5; round++)
            {
                BanEntry? ban = null;
                while (ban == null)
                {
                    ban = guard.CountConnection(Client);
                }

                durations.Add(ban.Duration);
                Assert.NotNull(guard.FindBan(Client));

                _clock.Advance(ban.Duration + TimeSpan.FromSeconds(1));
                Assert.Null(guard.FindBan(Client));
            }

            Assert.Equal([10, 20, 40, 60, 60], durations.Select(o => (int)o.TotalMinutes));
        }

        [Fact]
        public void An_address_that_behaved_for_long_enough_starts_over()
        {
            var guard = Guard(new() { ["MaxConnections"] = "2", ["BanMinutes"] = "10", ["OffenceMemoryHours"] = "2" });

            BanEntry? ban = null;
            while (ban == null) { ban = guard.CountConnection(Client); }

            _clock.Advance(TimeSpan.FromHours(3));
            guard.Maintain();

            ban = null;
            while (ban == null) { ban = guard.CountConnection(Client); }

            Assert.Equal(1, ban.Offences);
            Assert.Equal(10, ban.Duration.TotalMinutes);
        }

        [Fact]
        public void An_allowed_address_is_never_found_banned()
        {
            var guard = Guard(new() { ["MaxConnections"] = "2", ["Allow"] = "203.0.113.0/24" });

            for (var i = 0; i < 5; i++)
            {
                guard.CountConnection(Client);
            }

            Assert.Null(guard.FindBan(Client));
        }

        [Fact]
        public void The_bans_are_saved_and_come_back()
        {
            var path = Path.Combine(Path.GetTempPath(), "antihammer-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                var guard = Guard(new() { ["MaxConnections"] = "2" });
                while (guard.CountConnection(Client) == null) { }
                guard.Bans.Save(path);

                var again = Guard(new() { ["MaxConnections"] = "2" });
                Assert.Equal(1, again.Bans.Load(path));
                Assert.NotNull(again.FindBan(Client));

                _clock.Advance(TimeSpan.FromDays(30));
                Assert.Equal(0, Guard().Bans.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void The_counters_of_idle_addresses_are_dropped()
        {
            var guard = Guard();

            for (var i = 0; i < 100; i++)
            {
                guard.CountRequest(IPAddress.Parse($"198.51.100.{i}"), "/");
            }

            Assert.True(guard.Rates.KeyCount >= 200);
            _clock.Advance(TimeSpan.FromHours(1));
            guard.Maintain();
            Assert.Equal(0, guard.Rates.KeyCount);
        }
    }
}
