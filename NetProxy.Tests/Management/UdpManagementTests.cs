using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// UDP proxies through the management API and the configuration files.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class UdpManagementTests(ManagementFixture service) : ManagementTest(service)
    {
        private NpProxyConfiguration NewUdpProxy(string name, UdpEchoBackend backend, Action<NpProxyConfiguration>? change = null)
            => ProxyBuilder.Configuration(name, Net.FreeUdpPort(), TrafficType.Udp, backend.Port, change);

        [Fact]
        public async Task A_udp_proxy_is_created_listed_and_serves()
        {
            var backend = Host.Own(new UdpEchoBackend("echo:"));
            var proxy = NewUdpProxy("dns", backend);

            var result = Client.ApplyProxy(proxy);

            Assert.True(result.Success, string.Join("; ", result.Problems));
            Assert.Equal("created", result.Action);
            Assert.Null(result.StartError);

            var listed = Assert.Single(Client.ListProxies());
            Assert.Equal(TrafficType.Udp, listed.TrafficType);
            Assert.StartsWith("Udp", listed.ProxyType);
            Assert.True(listed.IsRunning);

            using var client = new UdpTestClient(proxy.ListenPort);
            Assert.Equal("echo:hello", await client.Ask("hello"));
        }

        [Fact]
        public async Task The_sessions_are_listed_as_connections_and_can_be_closed()
        {
            var backend = Host.Own(new UdpEchoBackend());
            var proxy = NewUdpProxy("dns", backend);
            Client.ApplyProxy(proxy);
            var id = Client.GetProxy("dns")!.Id;

            using var first = new UdpTestClient(proxy.ListenPort);
            using var second = new UdpTestClient(proxy.ListenPort);
            await first.Ask("one");
            await second.Ask("two");

            var connections = Client.GetConnections(id);
            Assert.Equal(2, connections.Count);
            Assert.All(connections, o =>
            {
                Assert.Equal("Udp", o.Protocol);
                Assert.Equal(backend.Port, o.DestinationPort);
                Assert.True(o.IsConnectedToEndpoint);
            });
            Assert.Equal(2, Client.GetStatistics()[id].CurrentConnections);

            Assert.Equal(1, Client.CloseConnections(connections[0].Id, null));
            Assert.Equal(1, await WaitForSessions(id, 1));

            Assert.Equal(1, Client.CloseConnections(null, "dns"));
            Assert.Equal(0, await WaitForSessions(id, 0));

            Assert.Equal("again", await first.Ask("again")); //And a client that comes back gets a new session.
        }

        private async Task<int> WaitForSessions(Guid proxyId, int expected)
            => await NetProxy.Tests.Support.Client.Eventually(() => Task.FromResult(Client.GetConnections(proxyId).Count), o => o == expected, seconds: 8);

        [Fact]
        public void What_a_udp_proxy_cannot_use_is_refused_with_the_reason()
        {
            var backend = Host.Own(new UdpEchoBackend());
            var proxy = NewUdpProxy("dns", backend, c =>
            {
                c.HostNames.Add(new NpHostName { Host = "a.example.com" });
                c.ForwardedHeaders = ForwardedHeadersMode.Replace;
            });

            var result = Client.ApplyProxy(proxy);

            Assert.False(result.Success);
            Assert.Contains(result.Problems, o => o.Contains("host names"));
            Assert.Contains(result.Problems, o => o.Contains("forwarded"));
            Assert.Empty(Client.ListProxies());
        }

        [Fact]
        public void Two_udp_proxies_cannot_use_a_port_but_a_tcp_proxy_can_have_the_same_number()
        {
            var backend = Host.Own(new UdpEchoBackend());
            var port = Net.FreePortForTcpAndUdp();
            var udp = NewUdpProxy("dns", backend);
            udp.ListenPort = port;
            Assert.True(Client.ApplyProxy(udp).Success);

            var second = NewUdpProxy("dns2", backend);
            second.ListenPort = port;
            var refused = Client.ApplyProxy(second);
            Assert.False(refused.Success);
            Assert.Contains(refused.Problems, o => o.Contains("UDP port"));

            var tcp = NewProxy("web");
            tcp.ListenPort = port;
            var allowed = Client.ApplyProxy(tcp);
            Assert.True(allowed.Success, string.Join("; ", allowed.Problems));
            Assert.Null(allowed.StartError);
        }

        [Fact]
        public void The_datagram_limit_survives_a_trip_through_the_service()
        {
            var backend = Host.Own(new UdpEchoBackend());
            Client.ApplyProxy(NewUdpProxy("dns", backend, c =>
            {
                c.MaxDatagramsPerSecondPerClient = 250;
                c.IdleTimeoutSeconds = 15;
            }));

            var stored = Client.GetProxy("dns")!;

            Assert.Equal(250, stored.MaxDatagramsPerSecondPerClient);
            Assert.Equal(15, stored.IdleTimeoutSeconds);
            Assert.Equal(TrafficType.Udp, Client.Export().Proxies.Single().TrafficType);
        }

        [Fact]
        public async Task A_udp_proxy_written_in_the_file_by_hand_is_added_and_serves()
        {
            var backend = Host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            var file = Path.Combine(TestEnvironment.ConfigurationFolder, "NpProxyConfiguration.json");
            var proxies = File.Exists(file) ? JArray.Parse(File.ReadAllText(file)) : [];
            proxies.Add(JObject.Parse($$"""
                {
                  "Name": "dns-by-hand", "TrafficType": "Udp", "ListenPort": {{port}}, "AutoStart": true, "IdleTimeoutSeconds": 30,
                  "Bindings": [ { "Address": "127.0.0.1" } ],
                  "Endpoints": { "Collection": [ { "Address": "127.0.0.1", "Port": {{backend.Port}} } ] }
                }
                """));
            File.WriteAllText(file, proxies.ToString());

            var reply = Client.Reload();

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.Equal(["Proxy 'dns-by-hand': added."], reply.Changes);
            using var client = new UdpTestClient(port);
            Assert.Equal("echo:hi", await client.Ask("hi"));
        }

        [Fact]
        public async Task Stopping_and_starting_a_udp_proxy_through_the_api_works()
        {
            var backend = Host.Own(new UdpEchoBackend("echo:"));
            var proxy = NewUdpProxy("dns", backend);
            Client.ApplyProxy(proxy);
            using var client = new UdpTestClient(proxy.ListenPort);
            Assert.Equal("echo:a", await client.Ask("a"));

            Assert.Null(Client.ProxyAction("dns", NpProxyAction.Stop));
            Assert.Null(await client.Ask("b", 1));

            Assert.Null(Client.ProxyAction("dns", NpProxyAction.Start));
            Assert.Equal("echo:c", await client.Ask("c"));

            Assert.Null(Client.ProxyAction("dns", NpProxyAction.Restart));
            Assert.Equal("echo:d", await client.Ask("d"));

            Assert.Null(Client.ProxyAction("dns", NpProxyAction.Delete));
            Assert.Null(await client.Ask("e", 1));
        }
    }
}
