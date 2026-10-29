#pragma warning disable xUnit1031 //The plug-in events complete at once; there is nothing to deadlock on.
using NetProxy.Plugins.AntiHammer;
using NetProxy.Plugins.Common;
using NetProxy.Plugins.Tests.Support;
using NetProxy.PluginApi;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class AntiHammerPluginTests : IDisposable
    {
        private readonly FakeClock _clock = new();
        private readonly List<string> _directories = [];

        public void Dispose()
        {
            foreach (var directory in _directories)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private (AntiHammerPlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null, string? directory = null)
        {
            settings ??= [];
            settings.TryAdd("LogOnly", "false");

            var host = new FakeHost(settings, directory);
            Directory.CreateDirectory(host.DataDirectory);
            _directories.Add(host.DataDirectory);

            var plugin = new AntiHammerPlugin(_clock);
            plugin.Attach(host);
            plugin.OnServiceStartingAsync(CancellationToken.None).AsTask().Wait();
            return (plugin, host);
        }

        private static NpVerdict Connect(AntiHammerPlugin plugin, FakeConnection connection)
            => plugin.OnConnectingAsync(connection, CancellationToken.None).AsTask().Result;

        private static NpDataEventArgs Send(AntiHammerPlugin plugin, FakeConnection connection, byte[] bytes, bool canRespond = true)
        {
            var data = new NpDataEventArgs(connection, NpDataDirection.ClientToEndpoint, bytes, true, false, canRespond);
            plugin.OnDataReceivedAsync(data, CancellationToken.None).AsTask().Wait();
            return data;
        }

        [Fact]
        public void Log_only_is_the_default_and_bans_nobody()
        {
            var host = new FakeHost();
            Directory.CreateDirectory(host.DataDirectory);
            _directories.Add(host.DataDirectory);
            var plugin = new AntiHammerPlugin(_clock);
            plugin.Attach(host);
            plugin.OnServiceStartingAsync(CancellationToken.None).AsTask().Wait();

            var connection = new FakeConnection();
            var verdicts = Enumerable.Range(0, 150).Select(_ => Connect(plugin, connection)).ToList();

            Assert.All(verdicts, o => Assert.True(o.Allowed));
            Assert.Single(host.Texts, o => o.StartsWith("Would ban 203.0.113.7 "));

            var data = Send(plugin, connection, Http.Request("/index.html"));
            Assert.False(data.IsDropped);
            plugin.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();
            Assert.False(File.Exists(Path.Combine(host.DataDirectory, "bans.json"))); //Nothing is kept for bans that were not made.
        }

        [Fact]
        public void A_connection_flood_is_banned_and_turned_away_at_the_door()
        {
            var (plugin, host) = Start(new() { ["MaxConnections"] = "5" });
            var connection = new FakeConnection();

            for (var i = 0; i < 5; i++)
            {
                Assert.True(Connect(plugin, connection).Allowed);
            }

            var verdict = Connect(plugin, connection);
            Assert.False(verdict.Allowed);
            Assert.Contains("203.0.113.7", verdict.Reason);
            Assert.False(Connect(plugin, connection).Allowed);
            Assert.Single(host.Texts, o => o.StartsWith("Banned 203.0.113.7 for 1 hour(s)"));

            Assert.True(Connect(plugin, new FakeConnection { ClientAddress = "203.0.113.8" }).Allowed);

            _clock.Advance(TimeSpan.FromMinutes(61));
            Assert.True(Connect(plugin, connection).Allowed);
        }

        [Fact]
        public void Addresses_that_are_allowed_and_private_ones_are_never_turned_away()
        {
            var (plugin, _) = Start(new() { ["MaxConnections"] = "2", ["Allow"] = "203.0.113.0/24" });

            foreach (var address in new[] { "203.0.113.7", "192.168.1.5", "127.0.0.1" })
            {
                var connection = new FakeConnection { ClientAddress = address };
                Assert.All(Enumerable.Range(0, 20), _ => Assert.True(Connect(plugin, connection).Allowed));
            }
        }

        [Fact]
        public void A_request_flood_is_answered_with_a_403_and_the_connection_is_closed()
        {
            var (plugin, host) = Start(new() { ["MaxRequests"] = "5" });
            var connection = new FakeConnection();

            for (var i = 0; i < 5; i++)
            {
                Assert.False(Send(plugin, connection, Http.Request($"/page{i}")).IsDropped);
            }

            var data = Send(plugin, connection, Http.Request("/page6"));

            Assert.True(data.IsDropped);
            Assert.True(data.CloseAfterResponse);
            var text = Http.Text(data.Response);
            Assert.StartsWith("HTTP/1.1 403 Forbidden\r\n", text);
            Assert.Contains("Connection: close", text);
            Assert.Contains("Retry-After: 3600", text);
            Assert.Contains("more than 5 requests in 10 seconds", Assert.Single(host.Texts, o => o.StartsWith("Banned ")));

            //A new connection from the address does not get in.
            Assert.False(Connect(plugin, new FakeConnection()).Allowed);
        }

        [Fact]
        public void The_content_length_of_the_response_is_the_length_of_its_body()
        {
            var (plugin, _) = Start(new() { ["MaxRequests"] = "2" });
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/a"));
            Send(plugin, connection, Http.Request("/b"));
            var text = Http.Text(Send(plugin, connection, Http.Request("/c")).Response);

            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            var length = int.Parse(text.Split("Content-Length: ")[1].Split("\r\n")[0]);
            Assert.Equal(length, text.Length - split - 4);
        }

        [Fact]
        public void Probing_gets_the_address_banned_and_the_probe_never_reaches_the_endpoint()
        {
            var (plugin, host) = Start();
            var connection = new FakeConnection();

            Assert.False(Send(plugin, connection, Http.Request("/.env")).IsDropped);
            Assert.False(Send(plugin, connection, Http.Request("/wp-login.php")).IsDropped);
            Assert.True(Send(plugin, connection, Http.Request("/phpmyadmin/")).IsDropped);
            Assert.Contains("looking for pages that are not there", Assert.Single(host.Texts, o => o.StartsWith("Banned ")));

            //A banned address stays banned for the requests that follow on a connection that is still open.
            Assert.True(Send(plugin, connection, Http.Request("/index.html")).IsDropped);
        }

        [Fact]
        public void When_the_client_can_not_be_answered_the_connection_is_closed()
        {
            var (plugin, _) = Start(new() { ["MaxRequests"] = "2" });
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/a"), canRespond: false);
            Send(plugin, connection, Http.Request("/b"), canRespond: false);
            var data = Send(plugin, connection, Http.Request("/c"), canRespond: false);

            Assert.True(data.IsDropped);
            Assert.Null(data.Response);
            Assert.NotNull(data.CloseReason);
        }

        [Fact]
        public void Tcp_proxies_get_the_connection_limits_only_and_http2_is_left_alone()
        {
            var (plugin, host) = Start(new() { ["MaxRequests"] = "2" });
            var tcp = new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) };
            var h2 = new FakeConnection { IsHttp2 = true };

            for (var i = 0; i < 10; i++)
            {
                Assert.False(Send(plugin, tcp, Http.Request($"/{i}")).IsDropped);
                Assert.False(Send(plugin, h2, Http.Request($"/{i}")).IsDropped);
            }

            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));
        }

        [Fact]
        public void The_data_from_the_endpoint_is_not_looked_at()
        {
            var (plugin, _) = Start(new() { ["MaxRequests"] = "2" });
            var connection = new FakeConnection();

            for (var i = 0; i < 10; i++)
            {
                var data = new NpDataEventArgs(connection, NpDataDirection.EndpointToClient, Http.Request($"/{i}"), false, true);
                plugin.OnDataReceivedAsync(data, CancellationToken.None).AsTask().Wait();
                Assert.False(data.IsDropped);
            }
        }

        //---------------------------------------------------------------- Behind a proxy that is trusted

        private static FakeConnection Cloudflare(string edge = "104.16.1.1") => new() { ClientAddress = edge };

        private static byte[] ThroughCloudflare(string target, string client, string extraHeaders = "")
            => Http.Request(target, extraHeaders: $"CF-Connecting-IP: {client}\r\nX-Forwarded-For: {client}\r\n" + extraHeaders);

        [Fact]
        public void Behind_cloudflare_the_client_is_banned_and_not_cloudflare()
        {
            var (plugin, host) = Start(new() { ["TrustedProxies"] = "cloudflare", ["MaxRequests"] = "5" });
            var edge = Cloudflare();

            //Cloudflare itself is never turned away, however much comes through it.
            for (var i = 0; i < 300; i++)
            {
                Assert.True(Connect(plugin, edge).Allowed);
            }

            for (var i = 0; i < 5; i++)
            {
                Assert.False(Send(plugin, edge, ThroughCloudflare($"/p{i}", "198.51.100.9")).IsDropped);
            }

            var data = Send(plugin, edge, ThroughCloudflare("/p6", "198.51.100.9"));
            Assert.True(data.IsDropped);
            Assert.False(data.CloseAfterResponse); //The connection is shared with other clients of Cloudflare.
            Assert.Contains("Connection: keep-alive", Http.Text(data.Response));
            Assert.Single(host.Texts, o => o.StartsWith("Banned 198.51.100.9 "));

            //Another client on the same connection carries on, and so does the one that Cloudflare connects from.
            Assert.False(Send(plugin, edge, ThroughCloudflare("/p", "198.51.100.10")).IsDropped);
            Assert.True(Connect(plugin, edge).Allowed);
        }

        [Fact]
        public void A_banned_client_with_a_body_to_come_closes_the_connection()
        {
            var (plugin, _) = Start(new() { ["TrustedProxies"] = "cloudflare", ["MaxRequests"] = "2" });
            var edge = Cloudflare();

            Send(plugin, edge, ThroughCloudflare("/a", "198.51.100.9"));
            Send(plugin, edge, ThroughCloudflare("/b", "198.51.100.9"));

            var head = Http.Request("/upload", "POST", extraHeaders: "CF-Connecting-IP: 198.51.100.9\r\n", body: "0123456789");
            var data = Send(plugin, edge, head.AsSpan(0, head.Length - 5).ToArray());

            Assert.True(data.IsDropped);
            Assert.True(data.CloseAfterResponse);
        }

        [Fact]
        public void The_headers_of_a_client_that_is_not_a_trusted_proxy_are_not_believed()
        {
            var (plugin, host) = Start(new() { ["TrustedProxies"] = "cloudflare", ["MaxRequests"] = "3" });
            var liar = new FakeConnection { ClientAddress = "203.0.113.50" };

            //It says it is somebody else, to get that address banned instead of itself.
            for (var i = 0; i < 4; i++)
            {
                Send(plugin, liar, ThroughCloudflare($"/p{i}", "198.51.100.77"));
            }

            Assert.Single(host.Texts, o => o.StartsWith("Banned 203.0.113.50 "));
            Assert.DoesNotContain(host.Texts, o => o.Contains("198.51.100.77"));
        }

        [Fact]
        public void X_forwarded_for_is_read_from_the_right_and_skips_the_proxies_that_are_trusted()
        {
            var (plugin, _) = Start(new() { ["TrustedProxies"] = "cloudflare" });
            var peer = System.Net.IPAddress.Parse("104.16.1.1");

            SeenRequest Request(string forwardedFor) => new("GET", "/", null, forwardedFor, null, false);

            //The client sent "6.6.6.6" itself; the proxy added the address that it saw.
            Assert.Equal("198.51.100.9", plugin.ClientBehindProxy(peer, Request("6.6.6.6, 198.51.100.9, 172.67.1.1")).ToString());
            Assert.Equal("198.51.100.9", plugin.ClientBehindProxy(peer, Request("198.51.100.9")).ToString());
            Assert.Equal("104.16.1.1", plugin.ClientBehindProxy(peer, Request("garbage")).ToString());
            Assert.Equal("104.16.1.1", plugin.ClientBehindProxy(peer, new SeenRequest("GET", "/", null, null, null, false)).ToString());
            Assert.Equal("9.9.9.9", plugin.ClientBehindProxy(peer, new SeenRequest("GET", "/", null, "198.51.100.9", "9.9.9.9", false)).ToString());
        }

        [Fact]
        public void An_allowed_client_behind_cloudflare_is_not_banned()
        {
            var (plugin, _) = Start(new() { ["TrustedProxies"] = "cloudflare", ["MaxRequests"] = "2", ["Allow"] = "198.51.100.9" });
            var edge = Cloudflare();

            for (var i = 0; i < 20; i++)
            {
                Assert.False(Send(plugin, edge, ThroughCloudflare($"/p{i}", "198.51.100.9")).IsDropped);
            }
        }

        //---------------------------------------------------------------- Keeping the bans

        [Fact]
        public void Bans_survive_a_restart()
        {
            var directory = Path.Combine(Path.GetTempPath(), "antihammer-" + Guid.NewGuid().ToString("N"));
            var (first, _) = Start(new() { ["MaxConnections"] = "2" }, directory);
            var connection = new FakeConnection();

            while (Connect(first, connection).Allowed) { }

            first.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();
            Assert.True(File.Exists(Path.Combine(directory, "bans.json")));

            var (second, host) = Start(new() { ["MaxConnections"] = "2" }, directory);
            Assert.False(Connect(second, connection).Allowed);
            Assert.Contains(host.Texts, o => o.Contains("1 ban(s) from before"));
        }

        [Fact]
        public void The_tick_saves_and_reports_what_it_turned_away_and_a_broken_file_is_survived()
        {
            var directory = Path.Combine(Path.GetTempPath(), "antihammer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "bans.json"), "{ this is not json");

            var (plugin, host) = Start(new() { ["MaxConnections"] = "2" }, directory);
            Assert.Contains(host.Messages, o => o.Level == NpPluginLogLevel.Warning && o.Message.Contains("could not be read"));

            var connection = new FakeConnection();
            while (Connect(plugin, connection).Allowed) { }
            Connect(plugin, connection);

            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            Assert.Contains(host.Texts, o => o.StartsWith("Turned away 2 connection(s)"));
            Assert.Contains("203.0.113.7", File.ReadAllText(Path.Combine(directory, "bans.json")));
        }

        [Fact]
        public void An_address_that_is_allowed_later_is_no_longer_banned_even_if_a_ban_was_kept()
        {
            var directory = Path.Combine(Path.GetTempPath(), "antihammer-" + Guid.NewGuid().ToString("N"));
            var (first, _) = Start(new() { ["MaxConnections"] = "2" }, directory);
            var connection = new FakeConnection();
            while (Connect(first, connection).Allowed) { }
            first.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();

            var (second, _) = Start(new() { ["Allow"] = "203.0.113.7" }, directory);
            Assert.True(Connect(second, connection).Allowed);
        }
    }
}
