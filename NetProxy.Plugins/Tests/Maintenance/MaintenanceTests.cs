using NetProxy.Plugins.Common;
using NetProxy.Plugins.Maintenance;
using NetProxy.Plugins.Tests.Support;
using NetProxy.PluginApi;
using System.Net;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class MaintenanceTests : IDisposable
    {
        private readonly FakeClock _clock = new();
        private readonly List<string> _directories = [];

        public void Dispose()
        {
            foreach (var directory in _directories.Where(Directory.Exists))
            {
                Directory.Delete(directory, true);
            }
        }

        private (MaintenancePlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
        {
            var host = new FakeHost(settings);
            Directory.CreateDirectory(host.DataDirectory);
            _directories.Add(host.DataDirectory);

            var plugin = new MaintenancePlugin(_clock);
            plugin.Attach(host);
            plugin.OnServiceStartingAsync(CancellationToken.None).AsTask().Wait();
            return (plugin, host);
        }

        private static NpUnavailableEventArgs Unavailable(MaintenancePlugin plugin, FakeConnection? connection = null, bool canRespond = true)
        {
            connection ??= new FakeConnection();
            var failure = new NpEndpointFailure(new NpEndpointInfo(Guid.NewGuid(), "10.0.0.5", 8080, false), "refused");
            var e = new NpUnavailableEventArgs(connection, [failure], canRespond);
            plugin.OnEndpointsUnavailableAsync(e, CancellationToken.None).AsTask().Wait();
            return e;
        }

        private static NpDataEventArgs Send(MaintenancePlugin plugin, FakeConnection connection, bool canRespond = true)
        {
            var data = new NpDataEventArgs(connection, NpDataDirection.ClientToEndpoint, Http.Request("/index.html"), true, false, canRespond);
            plugin.OnDataReceivedAsync(data, CancellationToken.None).AsTask().Wait();
            return data;
        }

        private static (string Head, string Body) Split(ReadOnlyMemory<byte>? response)
        {
            var bytes = response!.Value.ToArray();
            var split = System.Text.Encoding.Latin1.GetString(bytes).IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return (System.Text.Encoding.Latin1.GetString(bytes, 0, split), System.Text.Encoding.UTF8.GetString(bytes, split + 4, bytes.Length - split - 4));
        }

        [Fact]
        public void With_no_settings_the_client_gets_a_503_with_a_page()
        {
            var (plugin, _) = Start();

            var (head, body) = Split(Unavailable(plugin).Response);

            Assert.StartsWith("HTTP/1.1 503 Service Unavailable\r\n", head);
            Assert.Contains("Content-Type: text/html; charset=utf-8", head);
            Assert.Contains("Retry-After: 120", head);
            Assert.Contains("Cache-Control: no-store", head);
            Assert.Contains("Connection: close", head);
            Assert.Contains("<h1>We&#39;ll be right back</h1>", body);
            Assert.Contains("down for maintenance", body);
        }

        [Fact]
        public void The_content_length_is_the_length_of_the_body_in_bytes()
        {
            var (plugin, _) = Start(new() { ["Title"] = "Zurück bald", ["Message"] = "Wartung ✓" });

            var (head, body) = Split(Unavailable(plugin).Response);

            var length = int.Parse(head.Split("Content-Length: ")[1].Split("\r\n")[0]);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(body), length);
            Assert.Contains("Zur&#252;ck bald", body); //Encoded, so that it is right whatever the encoding of the page.
        }

        [Fact]
        public void The_text_and_status_can_be_set_and_the_text_is_made_safe()
        {
            var (plugin, _) = Start(new()
            {
                ["StatusCode"] = "502", ["RetryAfterSeconds"] = "0", ["Title"] = "<b>Back</b> at 5",
                ["Message"] = "Line one\nLine <script>alert(1)</script>"
            });

            var (head, body) = Split(Unavailable(plugin).Response);

            Assert.StartsWith("HTTP/1.1 502 Bad Gateway\r\n", head);
            Assert.DoesNotContain("Retry-After", head);
            Assert.DoesNotContain("<script>", body);
            Assert.DoesNotContain("<b>", body);
            Assert.Contains("&lt;b&gt;Back&lt;/b&gt; at 5", body);
            Assert.Contains("Line one<br>Line &lt;script&gt;", body);
        }

        [Fact]
        public void A_page_file_is_served_as_it_is_and_is_read_again_when_it_changes()
        {
            var host = new FakeHost();
            Directory.CreateDirectory(host.DataDirectory);
            _directories.Add(host.DataDirectory);
            var file = Path.Combine(host.DataDirectory, "down.html");
            File.WriteAllText(file, "<p>first</p>");

            var settings = new Dictionary<string, string> { ["PageFile"] = "down.html", ["ContentType"] = "text/html" };
            var plugin = new MaintenancePlugin(_clock);
            plugin.Attach(new FakeHost(settings, host.DataDirectory));
            plugin.OnServiceStartingAsync(CancellationToken.None).AsTask().Wait();

            Assert.Equal("<p>first</p>", Split(Unavailable(plugin).Response).Body);

            File.WriteAllText(file, "<p>second, and longer</p>");
            Assert.Equal("<p>first</p>", Split(Unavailable(plugin).Response).Body); //Not looked at again straight away.

            _clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("<p>second, and longer</p>", Split(Unavailable(plugin).Response).Body);
        }

        [Fact]
        public void A_page_file_that_is_missing_falls_back_to_the_built_page_and_says_so_once()
        {
            var (plugin, host) = Start(new() { ["PageFile"] = "nothing-here.html" });

            Assert.Contains("<h1>", Split(Unavailable(plugin).Response).Body);
            Unavailable(plugin);

            Assert.Single(host.Messages, o => o.Level == NpPluginLogLevel.Warning && o.Message.Contains("does not exist"));
        }

        [Fact]
        public void Bad_settings_are_reported_and_replaced_by_the_default()
        {
            var (plugin, host) = Start(new() { ["StatusCode"] = "200", ["RetryAfterSeconds"] = "soon", ["Force"] = "maybe", ["Allow"] = "nonsense" });

            Assert.Equal(4, host.Messages.Count(o => o.Level == NpPluginLogLevel.Warning));
            Assert.StartsWith("HTTP/1.1 503", Http.Text(Unavailable(plugin).Response));
        }

        [Fact]
        public void The_proxy_is_named_in_the_log_once_a_minute_and_not_for_every_client()
        {
            var (plugin, host) = Start();
            var connection = new FakeConnection();

            for (var i = 0; i < 20; i++)
            {
                Unavailable(plugin, connection);
            }

            Assert.Single(host.Texts, o => o.Contains("No endpoint of proxy 'web' works"));

            _clock.Advance(TimeSpan.FromMinutes(2));
            Unavailable(plugin, connection);
            Assert.Equal(2, host.Texts.Count(o => o.Contains("No endpoint of proxy 'web' works")));
        }

        [Fact]
        public void Tcp_proxies_and_http2_clients_are_not_given_a_page()
        {
            var (plugin, host) = Start();

            var tcp = new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) };
            Assert.Null(Unavailable(plugin, tcp).Response);

            Assert.Null(Unavailable(plugin, new FakeConnection { IsHttp2 = true }, canRespond: false).Response);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));
        }

        //---------------------------------------------------------------- Force

        [Fact]
        public void Without_force_the_requests_go_through()
        {
            var (plugin, _) = Start();

            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void With_force_every_request_is_answered_and_the_connection_closed()
        {
            var (plugin, host) = Start(new() { ["Force"] = "true", ["Message"] = "Upgrading the database." });

            var data = Send(plugin, new FakeConnection());

            Assert.True(data.IsDropped);
            Assert.True(data.CloseAfterResponse);
            Assert.Contains("Upgrading the database.", Split(data.Response).Body);
            Assert.Contains(host.Messages, o => o.Level == NpPluginLogLevel.Warning && o.Message.StartsWith("Maintenance mode is ON"));
        }

        [Fact]
        public void With_force_the_addresses_that_are_allowed_still_get_the_site()
        {
            var (plugin, _) = Start(new() { ["Force"] = "true", ["Allow"] = "198.51.100.0/24, 203.0.113.9" });

            Assert.False(Send(plugin, new FakeConnection { ClientAddress = "198.51.100.77" }).IsDropped);
            Assert.False(Send(plugin, new FakeConnection { ClientAddress = "203.0.113.9" }).IsDropped);
            Assert.True(Send(plugin, new FakeConnection { ClientAddress = "203.0.113.10" }).IsDropped);
        }

        [Fact]
        public void Force_leaves_the_answers_of_the_endpoint_and_tcp_proxies_alone_and_does_not_let_http2_through()
        {
            var (plugin, host) = Start(new() { ["Force"] = "true" });

            var fromEndpoint = new NpDataEventArgs(new FakeConnection(), NpDataDirection.EndpointToClient, Http.Request("/"), false, true);
            plugin.OnDataReceivedAsync(fromEndpoint, CancellationToken.None).AsTask().Wait();
            Assert.False(fromEndpoint.IsDropped);

            Assert.False(Send(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) }).IsDropped);

            //A client that can not be given the page does not get to a site that is meant to be down either.
            var h2 = Send(plugin, new FakeConnection { IsHttp2 = true }, canRespond: false);
            Assert.True(h2.IsDropped);
            Assert.NotNull(h2.CloseReason);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));
        }

        //---------------------------------------------------------------- Planned windows

        [Fact]
        public void A_window_switches_the_page_on_and_off_by_itself_and_tells_when_to_come_back()
        {
            var (plugin, host) = Start(new() { ["Window1"] = "2026-01-01 13:00 to 2026-01-01 15:00" }); //The clock is at 12:00 UTC.

            Assert.False(Send(plugin, new FakeConnection()).IsDropped);

            _clock.Advance(TimeSpan.FromMinutes(90)); //13:30.
            var data = Send(plugin, new FakeConnection());
            Assert.True(data.IsDropped);
            Assert.Contains("Retry-After: 5400", Split(data.Response).Head); //Until 15:00.

            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            Assert.Contains(host.Messages, o => o.Level == NpPluginLogLevel.Warning && o.Message.StartsWith("The planned maintenance window has started") && o.Message.Contains("2026-01-01 15:00:00Z"));
            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            Assert.Single(host.Texts, o => o.StartsWith("The planned maintenance window has started"));

            _clock.Advance(TimeSpan.FromHours(2)); //15:30.
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            Assert.Contains(host.Texts, o => o.StartsWith("The planned maintenance window has ended"));
        }

        [Fact]
        public void A_weekly_and_a_daily_window_come_again_and_again()
        {
            //The clock starts on a Thursday, the first of January 2026.
            var (plugin, _) = Start(new() { ["Weekly1"] = "Sat, Sun 02:00-04:00", ["Daily1"] = "23:00-23:30" });

            Assert.False(Send(plugin, new FakeConnection()).IsDropped); //Thursday 12:00.

            _clock.Advance(TimeSpan.FromHours(11).Add(TimeSpan.FromMinutes(15))); //Thursday 23:15.
            Assert.True(Send(plugin, new FakeConnection()).IsDropped);

            _clock.Advance(TimeSpan.FromHours(3)); //Friday 02:15: not a weekend.
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);

            _clock.Advance(TimeSpan.FromDays(1)); //Saturday 02:15.
            Assert.True(Send(plugin, new FakeConnection()).IsDropped);

            _clock.Advance(TimeSpan.FromHours(2)); //Saturday 04:15.
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void A_window_that_goes_over_midnight_is_on_on_both_sides_of_it()
        {
            var (plugin, _) = Start(new() { ["Daily1"] = "22:00-02:00" });

            Assert.False(Send(plugin, new FakeConnection()).IsDropped); //12:00.
            _clock.Advance(TimeSpan.FromHours(11)); //23:00.
            Assert.True(Send(plugin, new FakeConnection()).IsDropped);
            _clock.Advance(TimeSpan.FromHours(2)); //01:00 the next day.
            Assert.True(Send(plugin, new FakeConnection()).IsDropped);
            _clock.Advance(TimeSpan.FromHours(2)); //03:00.
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void The_times_of_a_window_are_in_the_time_zone_that_is_set()
        {
            var zone = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(o => o.BaseUtcOffset == TimeSpan.FromHours(2) && o.SupportsDaylightSavingTime == false)
                ?? TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(o => o.BaseUtcOffset == TimeSpan.FromHours(5.5));
            if (zone == null)
            {
                return; //This machine has no zone without daylight saving to test with.
            }

            var offset = zone.BaseUtcOffset;
            var (plugin, _) = Start(new() { ["TimeZone"] = zone.Id, ["Daily1"] = $"{(12 + offset.Hours):00}:{offset.Minutes:00}-{(13 + offset.Hours):00}:{offset.Minutes:00}" }); //12:00 UTC is this local time.

            Assert.True(Send(plugin, new FakeConnection()).IsDropped);
            _clock.Advance(TimeSpan.FromHours(2));
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void Windows_that_are_wrong_are_reported_and_left_out()
        {
            var (plugin, host) = Start(new()
            {
                ["Window1"] = "yesterday to tomorrow", ["Window2"] = "2026-01-02 10:00 to 2026-01-02 09:00", ["Weekly1"] = "Someday 02:00-04:00", ["Weekly2"] = "Sun 2am-4am",
                ["Daily1"] = "03:00", ["Daily2"] = "04:00-04:00", ["TimeZone"] = "Mars/Olympus"
            });

            Assert.Equal(7, host.Messages.Count(o => o.Level == NpPluginLogLevel.Warning));
            Assert.False(Send(plugin, new FakeConnection()).IsDropped);
        }

        [Fact]
        public void The_log_says_when_the_next_window_starts()
        {
            var (_, host) = Start(new() { ["Weekly1"] = "Sat 02:00-04:00" });

            Assert.Contains(host.Texts, o => o.StartsWith("Planned windows: Sat 02:00-04:00. The next starts 2026-01-03 02:00:00Z."));
        }

        [Fact]
        public void Address_ranges_know_what_is_in_them()
        {
            Assert.True(AddressRange.TryParse("10.0.0.0/8", out var range));
            Assert.True(range!.Contains(IPAddress.Parse("10.200.1.1")));
            Assert.True(range.Contains(IPAddress.Parse("::ffff:10.1.1.1")));
            Assert.False(range.Contains(IPAddress.Parse("11.0.0.1")));
            Assert.False(AddressRange.TryParse("10.0.0.0/40", out _));
        }
    }
}
