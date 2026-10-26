using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service;
using NetProxy.Tests.Support;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Plugins
{
    /// <summary>
    /// Plugins in the service, called from real proxies with real traffic.
    /// </summary>
    [Collection(ServiceCollection.Name)]
    public class PluginProxyTests : IDisposable
    {
        private readonly TestHost _host = new();
        private readonly List<Guid> _plugins = [];

        public PluginProxyTests() => TestEnvironment.ResetConfiguration();

        public void Dispose()
        {
            //The proxies first, then the plugins (which are told, and write to files in the folder of the host), then the folder.
            _host.StopProxies();

            foreach (var id in _plugins)
            {
                Singletons.Plugins.DeleteAsync(id).GetAwaiter().GetResult();
            }

            _host.Dispose();

            TestEnvironment.ResetConfiguration();
        }

        private async Task<NpPluginConfiguration> Plugin(string name, Dictionary<string, string>? settings = null, int timeoutMilliseconds = 5000)
        {
            var configuration = PluginSupport.Recorder(name, _host.Directory, settings, timeoutMilliseconds);
            Assert.Null(await Singletons.Plugins.UpsertAsync(configuration));
            _plugins.Add(configuration.Id);
            return configuration;
        }

        private List<string> Events(string name) => PluginSupport.Events(_host.Directory, name);

        private static async Task<List<string>> EventsWhen(Func<List<string>> events, Func<List<string>, bool> done)
            => await Client.Eventually(() => Task.FromResult(events()), done, seconds: 5);

        private NpProxyConfiguration Proxy(int port, int backendPort, TrafficType type = TrafficType.Http, Action<NpProxyConfiguration>? change = null, params NpPluginConfiguration[] plugins)
            => ProxyBuilder.Configuration("proxy", port, type, backendPort, c =>
            {
                c.PluginIds.AddRange(plugins.Select(o => o.Id));
                change?.Invoke(c);
            });

        //---------------------------------------------------------------- Opting in

        [Fact]
        public async Task A_proxy_that_did_not_choose_a_plugin_does_not_call_it()
        {
            await Plugin("rec");
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port));

            Assert.Contains("backend=A", await Client.Http(port));
            await Task.Delay(300);

            Assert.DoesNotContain(Events("rec"), o => o.StartsWith("Connecting") || o.StartsWith("ProxyStarting"));
        }

        [Fact]
        public async Task A_plugin_that_is_disabled_is_not_called_even_when_the_proxy_chose_it()
        {
            var plugin = await Plugin("rec");
            plugin.Enabled = false;
            Assert.Null(await Singletons.Plugins.UpsertAsync(plugin));
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            Assert.Contains("backend=A", await Client.Http(port));
            Assert.DoesNotContain(Events("rec"), o => o.StartsWith("Connecting"));
        }

        //---------------------------------------------------------------- The life of a connection

        [Fact]
        public async Task The_events_of_a_proxy_and_a_connection_come_in_order()
        {
            var plugin = await Plugin("rec");
            var port = Net.FreePort();
            var backend = _host.Backend("A");
            var proxy = _host.Start(Proxy(port, backend.Port, plugins: plugin));

            Assert.Contains("backend=A", await Client.Http(port, "www.example.com"));
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));
            proxy.Stop();
            events = Events("rec");

            string[] order =
            [
                "ProxyStarting proxy Http", "ProxyStarted proxy", "Connecting 127.0.0.1 proxy=proxy", "EndpointConnecting", "EndpointConnected",
                "Connected endpoint=" + backend.Port, "DataReceived ClientToEndpoint", "DataSending ClientToEndpoint", "DataSent ClientToEndpoint",
                "DataReceived EndpointToClient", "Disconnecting reason=", "Disconnected reason=Completed", "ProxyStopping proxy", "ProxyStopped proxy"
            ];

            var position = 0;
            foreach (var expected in order)
            {
                var found = events.FindIndex(position, o => o.StartsWith(expected));
                Assert.True(found >= 0, $"'{expected}' did not come after position {position}.\n{string.Join("\n", events)}");
                position = found + 1;
            }
        }

        [Fact]
        public async Task The_connection_knows_its_bytes_and_the_host_it_was_asked_for()
        {
            var plugin = await Plugin("rec");
            var port = Net.FreePort();
            var shared = _host.Start(ProxyBuilder.Configuration("a", port, TrafficType.Http, _host.Backend("A").Port, c =>
            {
                c.PluginIds.Add(plugin.Id);
                c.HostNames.Add(new NpHostName { Host = "a.example.com" });
            }));
            _host.Start(ProxyBuilder.Configuration("b", port, TrafficType.Http, _host.Backend("B").Port, c => c.HostNames.Add(new NpHostName { Host = "b.example.com" })));

            await Client.Http(port, "a.example.com");
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains(events, o => o.StartsWith("Connecting") && o.Contains("host=a.example.com"));
            Assert.True(events.Any(o => o.StartsWith("Disconnected ")), string.Join(" | ", events));
            var disconnected = Assert.Single(events, o => o.StartsWith("Disconnected "));
            Assert.DoesNotContain("in=0 ", disconnected);
            Assert.DoesNotContain("out=0", disconnected);
        }

        [Fact]
        public async Task A_plugin_can_refuse_a_connection_before_the_endpoint_is_contacted()
        {
            var plugin = await Plugin("rec", new() { ["DenyClients"] = "127.0.0.1" });
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            Assert.Equal("", await Client.Http(port));
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains("Disconnected reason=Denied in=0 out=0", events);
            Assert.DoesNotContain(events, o => o.StartsWith("EndpointConnecting"));
        }

        [Fact]
        public async Task A_plugin_can_skip_an_endpoint_and_the_next_one_is_used()
        {
            var first = _host.Backend("FIRST");
            var second = _host.Backend("SECOND");
            var plugin = await Plugin("rec", new() { ["DenyEndpointPort"] = first.Port.ToString() });
            var port = Net.FreePort();
            _host.Start(Proxy(port, first.Port, TrafficType.Http, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                c.Endpoints.Add(ProxyBuilder.Endpoint(second.Port));
            }, plugin));

            Assert.Contains("backend=SECOND", await Client.Http(port));
        }

        [Fact]
        public async Task A_failed_endpoint_is_reported_and_the_next_one_works()
        {
            var down = Net.FreePort();
            var up = _host.Backend("UP");
            var plugin = await Plugin("rec");
            var port = Net.FreePort();
            _host.Start(Proxy(port, down, TrafficType.Http, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                c.Endpoints.Add(ProxyBuilder.Endpoint(up.Port));
            }, plugin));

            Assert.Contains("backend=UP", await Client.Http(port));

            var events = Events("rec");
            Assert.Contains($"EndpointFailed {down}", events);
            Assert.Contains($"EndpointConnected {up.Port}", events);
        }

        [Fact]
        public async Task When_no_endpoint_works_the_disconnect_says_so()
        {
            var plugin = await Plugin("rec");
            var port = Net.FreePort();
            _host.Start(Proxy(port, Net.FreePort(), plugins: plugin));

            await Client.Http(port);
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains(events, o => o.StartsWith("Disconnected reason=EndpointUnavailable"));
        }

        [Fact]
        public async Task A_connection_that_goes_idle_is_reported_and_closed()
        {
            var plugin = await Plugin("rec");
            var echo = _host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            _host.Start(Proxy(port, echo.Port, TrafficType.Tcp, c => c.IdleTimeoutSeconds = 1, plugin));

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.Contains("Idle", events);
            Assert.Contains(events, o => o.StartsWith("Disconnected reason=IdleTimeout"));
        }

        [Fact]
        public async Task A_connection_refused_at_a_limit_is_reported()
        {
            var plugin = await Plugin("rec");
            var echo = _host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            _host.Start(Proxy(port, echo.Port, TrafficType.Tcp, c => c.MaxConnections = 1, plugin));

            using var first = new TcpClient(); await first.ConnectAsync("127.0.0.1", port);
            await Task.Delay(300);
            using var second = new TcpClient(); await second.ConnectAsync("127.0.0.1", port);

            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Refused")));
            Assert.Contains("Refused 127.0.0.1", events);
        }

        //---------------------------------------------------------------- Proxy events

        [Fact]
        public async Task A_plugin_can_keep_a_proxy_from_starting()
        {
            var plugin = await Plugin("rec", new() { ["DenyProxyStart"] = "true" });
            var proxy = _host.Create(Proxy(Net.FreePort(), _host.Backend("A").Port, plugins: plugin));

            Assert.False(proxy.Start(out var error));
            Assert.Contains("rec", error);
            Assert.Contains("not today", error);
            Assert.False(proxy.IsRunning);
            Assert.Contains(Events("rec"), o => o.StartsWith("ProxyStartFailed proxy"));
        }

        [Fact]
        public async Task A_start_that_fails_for_another_reason_is_reported_to_the_plugin()
        {
            var plugin = await Plugin("rec");
            var taken = new TcpListener(System.Net.IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var port = ((System.Net.IPEndPoint)taken.LocalEndpoint).Port;
                Assert.False(_host.Create(Proxy(port, _host.Backend("A").Port, plugins: plugin)).Start(out _));
            }
            finally
            {
                taken.Stop();
            }

            Assert.Contains(Events("rec"), o => o.StartsWith("ProxyStartFailed proxy"));
        }

        //---------------------------------------------------------------- Data

        [Fact]
        public async Task A_plugin_changes_the_response_the_client_gets()
        {
            var plugin = await Plugin("rec", new() { ["Replace"] = "backend=A=>backend=CHANGED" });
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            Assert.Contains("backend=CHANGED", await Client.Http(port));
        }

        [Fact]
        public async Task A_plugin_changes_the_request_the_endpoint_gets()
        {
            var plugin = await Plugin("rec", new() { ["Replace"] = "/original=>/rewritten" });
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            Assert.Contains("path=[/rewritten]", await Client.Http(port, path: "/original"));
        }

        [Fact]
        public async Task Plugins_are_called_in_the_order_of_the_service_and_each_gets_the_work_of_the_one_before()
        {
            var changes = await Plugin("changes", new() { ["Replace"] = "backend=A=>backend=CHANGED" });
            var sees = await Plugin("sees");
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: [sees, changes])); //The proxy lists them the other way round.

            Assert.Contains("backend=CHANGED", await Client.Http(port));
            await Task.Delay(300);

            var response = Events("sees").Where(o => o.StartsWith("DataSending EndpointToClient")).ToList();
            Assert.Contains(response, o => o.Contains("backend=CHANGED"));
            Assert.DoesNotContain(response, o => o.Contains("backend=A"));
        }

        [Fact]
        public async Task A_plugin_sees_the_decrypted_traffic_of_an_https_proxy_and_it_is_encrypted_again_after()
        {
            var plugin = await Plugin("rec", new() { ["Replace"] = "backend=A=>backend=CHANGED" });
            var port = Net.FreePort();
            var certificate = Certs.WritePfx(_host.Directory, "p.test.local");
            _host.Start(Proxy(port, _host.Backend("A").Port, TrafficType.Https, c =>
            {
                c.TlsCertificate = Certs.Register(certificate);
            }, plugin));

            var (body, _) = await Client.Https(port, "p.test.local");
            var events = Events("rec");

            Assert.Contains("backend=CHANGED", body);
            Assert.Contains(events, o => o.StartsWith("ClientTls") && o.Contains("host=p.test.local"));
            Assert.Contains(events, o => o.StartsWith("DataReceived ClientToEndpoint enc=True->False") && o.Contains("GET / HTTP/1.1")); //Plain text.
            Assert.Contains(events, o => o.StartsWith("DataSending EndpointToClient enc=False->True"));
        }

        [Fact]
        public async Task A_plugin_sees_only_encrypted_bytes_on_a_tcp_proxy()
        {
            var plugin = await Plugin("rec");
            var tls = _host.Own(new TlsEchoBackend(Certs.PfxBytes("behind.test.local")));
            var port = Net.FreePort();
            _host.Start(Proxy(port, tls.Port, TrafficType.Tcp, null, plugin));

            var (body, certificate) = await Client.Https(port, "behind.test.local");
            Assert.Contains("hello from tls", body);
            Assert.Equal("behind.test.local", certificate);

            var received = Events("rec").Where(o => o.StartsWith("DataReceived ClientToEndpoint")).ToList();
            Assert.NotEmpty(received);
            Assert.DoesNotContain(received, o => o.Contains("GET / HTTP/1.1")); //It is the handshake and encrypted records.
        }

        [Fact]
        public async Task A_plugin_can_drop_data_and_the_connection_stays_up()
        {
            var plugin = await Plugin("rec", new() { ["DropOnData"] = "/secret" });
            var echo = _host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            _host.Start(Proxy(port, echo.Port, TrafficType.Tcp, null, plugin));

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync("/secret"u8.ToArray());
            await Task.Delay(300);
            await stream.WriteAsync("/public"u8.ToArray());

            var buffer = new byte[100];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var read = await stream.ReadAsync(buffer, timeout.Token);

            Assert.Equal("/public", Encoding.ASCII.GetString(buffer, 0, read));
        }

        [Fact]
        public async Task A_plugin_can_close_the_connection_from_a_data_event()
        {
            var plugin = await Plugin("rec", new() { ["CloseOnData"] = "GET /" });
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            var response = await Client.Http(port);
            var events = await EventsWhen(() => Events("rec"), e => e.Any(o => o.StartsWith("Disconnected")));

            Assert.DoesNotContain("backend=A", response);
            Assert.Contains(events, o => o.StartsWith("Disconnected reason=ClosedByPlugin"));
        }

        //---------------------------------------------------------------- Plugins that misbehave

        [Fact]
        public async Task A_plugin_that_throws_on_every_read_does_not_stop_the_traffic()
        {
            var plugin = await Plugin("rec", new() { ["Throw"] = "true" });
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));

            for (var i = 0; i < 3; i++)
            {
                Assert.Contains("backend=A", await Client.Http(port));
            }
        }

        [Fact]
        public async Task A_plugin_that_is_too_slow_is_cut_off_at_its_time_limit()
        {
            var slow = await Plugin("slow", new() { ["SleepMs"] = "4000" }, timeoutMilliseconds: 200);
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: slow));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await Client.Http(port);
            stopwatch.Stop();

            Assert.Contains("backend=A", response);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"{stopwatch.Elapsed}");
        }

        [Fact]
        public async Task Deleting_a_plugin_while_a_proxy_uses_it_does_not_break_the_proxy()
        {
            var plugin = await Plugin("rec");
            var port = Net.FreePort();
            _host.Start(Proxy(port, _host.Backend("A").Port, plugins: plugin));
            Assert.Contains("backend=A", await Client.Http(port));

            await Singletons.Plugins.DeleteAsync(plugin.Id);

            Assert.Contains("backend=A", await Client.Http(port));
        }
    }
}
