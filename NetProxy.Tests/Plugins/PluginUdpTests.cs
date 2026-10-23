using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service;
using NetProxy.Service.Proxy;
using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Plugins
{
    /// <summary>
    /// Plugins on a UDP proxy: a session is a connection, and every datagram is a data event.
    /// </summary>
    [Collection(ServiceCollection.Name)]
    public class PluginUdpTests : IDisposable
    {
        private readonly TestHost _host = new();
        private readonly List<Guid> _plugins = [];

        public PluginUdpTests() => TestEnvironment.ResetConfiguration();

        public void Dispose()
        {
            _host.StopProxies();

            foreach (var id in _plugins)
            {
                Singletons.Plugins.DeleteAsync(id).GetAwaiter().GetResult();
            }

            _host.Dispose();
            TestEnvironment.ResetConfiguration();
        }

        private async Task<NpPluginConfiguration> Plugin(string name, Dictionary<string, string>? settings = null)
        {
            var configuration = PluginSupport.Recorder(name, _host.Directory, settings);
            Assert.Null(await Singletons.Plugins.UpsertAsync(configuration));
            _plugins.Add(configuration.Id);
            return configuration;
        }

        private List<string> Events(string name) => PluginSupport.Events(_host.Directory, name);

        private static async Task<List<string>> EventsWhen(Func<List<string>> events, Func<List<string>, bool> done)
            => await Client.Eventually(() => Task.FromResult(events()), done, seconds: 8);

        private NpProxy Start(int port, int endpointPort, NpPluginConfiguration plugin, Action<NpProxyConfiguration>? change = null)
            => _host.Start(ProxyBuilder.Configuration("udp", port, TrafficType.Udp, endpointPort, c =>
            {
                c.PluginIds.Add(plugin.Id);
                change?.Invoke(c);
            }));

        [Fact]
        public async Task The_events_of_a_udp_session_come_in_order_and_the_data_events_come_per_datagram()
        {
            var plugin = await Plugin("rec");
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin, c => c.IdleTimeoutSeconds = 1);
            using var client = new UdpTestClient(port);

            Assert.Equal("ping", await client.Ask("ping"));
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            //The two directions run side by side, so each is checked in its own order.
            static void InOrder(List<string> events, params string[] order)
            {
                var position = 0;
                foreach (var expected in order)
                {
                    var found = events.FindIndex(position, o => o.StartsWith(expected));
                    Assert.True(found >= 0, $"'{expected}' did not come after position {position}.\n{string.Join("\n", events)}");
                    position = found + 1;
                }
            }

            InOrder(events, "ProxyStarting udp Udp", "ProxyStarted udp", "Connecting 127.0.0.1 proxy=udp host= ", "EndpointConnecting", $"EndpointConnected {backend.Port}",
                $"Connected endpoint={backend.Port}");
            InOrder(events, $"Connected endpoint={backend.Port}", "DataReceived ClientToEndpoint enc=False->False ping", "DataSending ClientToEndpoint enc=False->False ping",
                "DataSent ClientToEndpoint 4", "Idle", "Disconnecting reason=IdleTimeout", "Disconnected reason=IdleTimeout in=4 out=4");
            InOrder(events, $"Connected endpoint={backend.Port}", "DataReceived EndpointToClient enc=False->False ping", "DataSending EndpointToClient",
                "DataSent EndpointToClient 4", "Idle", "Disconnecting reason=IdleTimeout", "Disconnected reason=IdleTimeout in=4 out=4");
        }

        [Fact]
        public async Task A_plugin_changes_the_datagrams_in_both_directions()
        {
            var plugin = await Plugin("rec", new() { ["Replace"] = "cat=>dog" });
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin);
            using var client = new UdpTestClient(port);

            //The endpoint gets dog (changed on the way in) and answers dog, and the answer is not changed again, it has no cat.
            Assert.Equal("the dog sat", await client.Ask("the cat sat"));
        }

        [Fact]
        public async Task A_plugin_can_drop_a_datagram_and_the_session_goes_on()
        {
            var plugin = await Plugin("rec", new() { ["DropOnData"] = "secret" });
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin);
            using var client = new UdpTestClient(port);

            Assert.Null(await client.Ask("a secret", 1));
            Assert.Equal("public", await client.Ask("public"));
            Assert.Equal(1, backend.Received); //The dropped one never reached the endpoint.
        }

        [Fact]
        public async Task A_plugin_can_end_the_session_from_a_datagram()
        {
            var plugin = await Plugin("rec", new() { ["CloseOnData"] = "bye" });
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin);
            using var client = new UdpTestClient(port);

            await client.Ask("hello");
            await client.Send("bye");
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains(events, o => o.StartsWith("Disconnected reason=ClosedByPlugin"));
        }

        [Fact]
        public async Task A_plugin_can_refuse_a_client_and_is_not_asked_again_for_every_datagram()
        {
            var plugin = await Plugin("rec", new() { ["DenyClients"] = "127.0.0.1" });
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin);
            using var client = new UdpTestClient(port);

            for (var i = 0; i < 20; i++)
            {
                await client.Send($"try-{i}");
            }

            Assert.Null(await client.Receive(1));
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains("Disconnected reason=Denied in=0 out=0", events);
            Assert.Equal(1, events.Count(o => o.StartsWith("Connecting")));
            Assert.Equal(0, backend.Received);
        }

        [Fact]
        public async Task A_client_turned_away_at_a_limit_is_reported_to_the_plugin()
        {
            var plugin = await Plugin("rec");
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, plugin, c => c.MaxConnections = 1);
            using var first = new UdpTestClient(port);
            using var second = new UdpTestClient(port);

            await first.Ask("one");
            await second.Send("two");

            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Refused")));
            Assert.Contains("Refused 127.0.0.1", events);
        }

        [Fact]
        public async Task Stopping_the_proxy_ends_the_session_for_the_plugin_as_aborted()
        {
            var plugin = await Plugin("rec");
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = Start(port, backend.Port, plugin);
            using var client = new UdpTestClient(port);
            await client.Ask("x");

            proxy.Stop();
            var events = Events("rec");

            Assert.Contains(events, o => o.StartsWith("Disconnected reason=Aborted"));
            Assert.Contains("ProxyStopped udp", events);
        }

        [Fact]
        public async Task A_slow_plugin_holds_up_its_own_client_and_not_the_others()
        {
            var slow = await Plugin("slow", new() { ["SleepMs"] = "1500", ["DenyClients"] = "none" });
            var backend = _host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            Start(port, backend.Port, slow, c => { });

            using var first = new UdpTestClient(port);
            using var second = new UdpTestClient(port);

            //Both are slowed by the plugin, but they run side by side: two datagrams take about as long as one.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var answers = await Task.WhenAll(first.Ask("one", 6), second.Ask("two", 6));
            stopwatch.Stop();

            Assert.Equal(new string?[] { "one", "two" }, answers);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"{stopwatch.Elapsed}");
        }
    }
}
