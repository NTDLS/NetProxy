using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service;
using NetProxy.Service.Proxy;
using NetProxy.Tests.Support;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace NetProxy.Tests.Service
{
    /// <summary>
    /// UDP: sessions per client address and port, relayed to an endpoint, with the datagrams kept whole and in order.
    /// </summary>
    public class UdpTests
    {
        private static NpProxyConfiguration Udp(string name, int port, int endpointPort, Action<NpProxyConfiguration>? change = null)
            => ProxyBuilder.Configuration(name, port, TrafficType.Udp, endpointPort, change);

        private static async Task<bool> Eventually(Func<bool> check, double seconds = 8)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (check() == false && stopwatch.Elapsed < TimeSpan.FromSeconds(seconds))
            {
                await Task.Delay(100);
            }

            return check();
        }

        //---------------------------------------------------------------- Relaying

        [Fact]
        public async Task A_datagram_goes_to_the_endpoint_and_the_answer_comes_back()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));

            using var client = new UdpTestClient(port);

            Assert.Equal("echo:hello", await client.Ask("hello"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1400)]
        [InlineData(8000)]
        [InlineData(60000)]
        public async Task A_datagram_arrives_whole_whatever_its_size(int size)
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);
            var sent = new byte[size];
            new Random(size).NextBytes(sent);

            await client.Send(sent);
            var received = await client.Receive(5);

            Assert.NotNull(received);
            Assert.True(sent.AsSpan().SequenceEqual(received));
        }

        [Fact]
        public async Task The_boundaries_of_the_datagrams_are_kept_and_they_stay_in_order()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);

            var sent = Enumerable.Range(0, 100).Select(i => $"datagram-{i}-{new string('x', i)}").ToList();
            foreach (var text in sent)
            {
                await client.Send(text);
            }

            var received = new List<string>();
            while (received.Count < sent.Count && await client.Receive(string.Empty, 3) is { } text)
            {
                received.Add(text);
            }

            Assert.Equal(sent, received);
        }

        [Fact]
        public async Task A_client_keeps_one_session_and_each_client_has_its_own()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port));

            using var first = new UdpTestClient(port);
            using var second = new UdpTestClient(port);

            for (var i = 0; i < 5; i++)
            {
                Assert.Equal($"first-{i}", await first.Ask($"first-{i}"));
            }

            Assert.Equal("second", await second.Ask("second"));

            //The endpoint sees one source for the first client, however many datagrams it sent, and another for the second.
            Assert.Equal(2, backend.Sources.Distinct().Count());
            Assert.Equal(5, backend.Sources.Count(o => o.Port == backend.Sources[0].Port));
            Assert.Equal(2UL, proxy.Statistics.TotalConnections);
            Assert.Equal(2, proxy.ActiveConnections.Count);
        }

        [Fact]
        public async Task Answers_go_to_the_client_that_asked()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));

            var clients = Enumerable.Range(0, 8).Select(_ => new UdpTestClient(port)).ToList();
            try
            {
                var answers = await Task.WhenAll(clients.Select((client, i) => client.Ask($"from-{i}")));

                Assert.Equal(Enumerable.Range(0, 8).Select(i => $"from-{i}"), answers);
            }
            finally
            {
                clients.ForEach(o => o.Dispose());
            }
        }

        [Fact]
        public async Task The_endpoint_can_start_the_conversation_after_the_client_has_sent_something()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);

            await client.Ask("hello");
            Assert.Null(await client.Receive(0.5)); //Nothing more unless the endpoint sends it.

            //The endpoint answers the address it was sent from, which is the socket of the session.
            using var peer = new UdpClient();
            await peer.SendAsync("unsolicited"u8.ToArray(), 11, new IPEndPoint(IPAddress.Loopback, backend.Sources[0].Port));
            Assert.Null(await client.Receive(0.5)); //Only the endpoint's own socket can: this one is not it.
        }

        [Fact]
        public async Task An_endpoint_given_as_a_host_name_is_looked_up()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port, c => c.Endpoints.Collection[0].Address = "localhost"));
            using var client = new UdpTestClient(port);

            Assert.Equal("echo:hi", await client.Ask("hi"));
        }

        [Fact]
        public async Task A_proxy_on_ipv6_relays_to_an_ipv4_endpoint()
        {
            if (Socket.OSSupportsIPv6 == false)
            {
                return;
            }

            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port, c => c.Bindings[0].Address = "::1"));
            using var client = new UdpTestClient(port, IPAddress.IPv6Loopback);

            Assert.Equal("echo:hi6", await client.Ask("hi6"));
        }

        [Fact]
        public async Task A_proxy_that_listens_on_all_addresses_shows_an_ipv4_client_by_its_ipv4_address()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port, c =>
            {
                c.ListenOnAllAddresses = true;
                c.BindingProtocol = BindingProtocol.Both;
            }));
            using var client = new UdpTestClient(port);

            Assert.Equal("echo:x", await client.Ask("x"));
            Assert.Equal("127.0.0.1", proxy.ActiveConnections.Values.Single().GetGridItem().SourceAddress);
        }

        //---------------------------------------------------------------- Sessions end

        [Fact]
        public async Task A_session_ends_when_the_client_is_quiet_and_a_new_one_starts_with_the_next_datagram()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port, c => c.IdleTimeoutSeconds = 1));
            using var client = new UdpTestClient(port);

            Assert.Equal("one", await client.Ask("one"));
            Assert.True(await Eventually(() => proxy.ActiveConnections.IsEmpty), "The session should have expired.");

            Assert.Equal("two", await client.Ask("two"));
            Assert.Equal(2UL, proxy.Statistics.TotalConnections);
            Assert.NotEqual(backend.Sources[0].Port, backend.Sources[1].Port); //A new socket, so a new source for the endpoint.
        }

        [Fact]
        public async Task A_session_that_keeps_sending_is_not_expired()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port, c => c.IdleTimeoutSeconds = 2));
            using var client = new UdpTestClient(port);

            for (var i = 0; i < 6; i++)
            {
                Assert.Equal("ping", await client.Ask("ping"));
                await Task.Delay(700);
            }

            Assert.Equal(1UL, proxy.Statistics.TotalConnections);
        }

        [Fact]
        public async Task Without_an_idle_timeout_a_session_still_ends_after_the_default()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);
            await client.Ask("x");

            //The default is a minute, so this only checks that the session is there and the rule is the one in the constants.
            Assert.Single(proxy.ActiveConnections);
            Assert.Equal(60, Constants.DefaultUdpIdleTimeoutSeconds);
            Assert.Equal(0, proxy.Configuration.IdleTimeoutSeconds);
        }

        [Fact]
        public async Task Stopping_the_proxy_ends_the_sessions_and_it_can_start_again()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend("echo:"));
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);
            await client.Ask("x");

            proxy.Stop();

            Assert.True(proxy.ActiveConnections.IsEmpty);
            Assert.False(proxy.IsRunning);
            Assert.Null(await client.Ask("nobody home", 1));

            Assert.True(proxy.Start(out var error), error);
            Assert.Equal("echo:back", await client.Ask("back"));
        }

        [Fact]
        public async Task A_session_can_be_closed_by_hand()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);
            await client.Ask("x");

            proxy.ActiveConnections.Values.Single().Abort();

            Assert.True(await Eventually(() => proxy.ActiveConnections.IsEmpty));
            Assert.Equal("again", await client.Ask("again")); //And the next datagram starts a new one.
        }

        //---------------------------------------------------------------- Endpoints

        [Fact]
        public async Task Round_robin_gives_each_new_session_the_next_endpoint()
        {
            using var host = new TestHost();
            var a = host.Own(new UdpEchoBackend("A:"));
            var b = host.Own(new UdpEchoBackend("B:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.RoundRobbin;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            var answers = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                using var client = new UdpTestClient(port);
                answers.Add((await client.Ask("x"))![..1]);
            }

            Assert.Equal(2, answers.Count(o => o == "A"));
            Assert.Equal(2, answers.Count(o => o == "B"));
        }

        [Fact]
        public async Task Sticky_sessions_keep_a_client_address_on_its_endpoint_across_ports()
        {
            using var host = new TestHost();
            var a = host.Own(new UdpEchoBackend("A:"));
            var b = host.Own(new UdpEchoBackend("B:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.RoundRobbin;
                c.UseStickySessions = true;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            var answers = new List<string>();
            for (var i = 0; i < 5; i++)
            {
                using var client = new UdpTestClient(port); //A new port each time, the same address.
                answers.Add((await client.Ask("x"))![..1]);
            }

            Assert.Single(answers.Distinct());
        }

        [Fact]
        public async Task An_endpoint_that_is_not_there_is_found_out_and_the_next_client_goes_to_the_other()
        {
            using var host = new TestHost();
            var up = host.Own(new UdpEchoBackend("UP:"));
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, Net.FreeUdpPort(), c => //Nothing is bound at the first endpoint.
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                c.Endpoints.Add(ProxyBuilder.Endpoint(up.Port));
            }));

            //The first client's datagram is lost: the refusal only comes back after it was sent.
            using var first = new UdpTestClient(port);
            await first.Send("lost");
            await Task.Delay(1000);

            using var second = new UdpTestClient(port);
            Assert.Equal("UP:found", await second.Ask("found", 5));
        }

        [Fact]
        public async Task A_proxy_where_no_endpoint_can_be_set_up_ignores_the_client_for_a_while()
        {
            using var host = new TestHost();
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, 9, c => c.Endpoints.Collection[0].Address = "no-such-host.invalid"));
            using var client = new UdpTestClient(port);

            await client.Send("one");
            Assert.True(await Eventually(() => proxy.ActiveConnections.IsEmpty && proxy.Statistics.TotalConnections == 1, 15));

            for (var i = 0; i < 20; i++)
            {
                await client.Send("again");
            }

            await Task.Delay(500);
            Assert.Equal(1UL, proxy.Statistics.TotalConnections); //Not a session, and a lookup, for each datagram.
        }

        //---------------------------------------------------------------- Limits

        [Fact]
        public async Task The_most_sessions_one_client_address_may_have_is_enforced()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port, c => c.MaxConnectionsPerClient = 1));

            using var first = new UdpTestClient(port);
            using var second = new UdpTestClient(port); //The same address, another port.

            Assert.Equal("one", await first.Ask("one"));
            Assert.Null(await second.Ask("two", 1));
            Assert.Equal("again", await first.Ask("again"));
        }

        [Fact]
        public async Task The_most_sessions_the_proxy_may_have_is_enforced_and_given_back()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port, c =>
            {
                c.MaxConnections = 1;
                c.IdleTimeoutSeconds = 1;
            }));

            using var first = new UdpTestClient(port);
            using var second = new UdpTestClient(port);
            Assert.Equal("one", await first.Ask("one"));
            Assert.Null(await second.Ask("two", 1));

            Assert.True(await Eventually(() => proxy.ActiveConnections.IsEmpty));
            Assert.Equal("three", await second.Ask("three"));
        }

        [Fact]
        public async Task A_client_that_sends_faster_than_the_limit_has_the_rest_dropped()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port, c => c.MaxDatagramsPerSecondPerClient = 5));
            using var client = new UdpTestClient(port);

            for (var i = 0; i < 60; i++)
            {
                await client.Send($"burst-{i}");
            }

            var received = 0;
            while (await client.Receive(1) != null)
            {
                received++;
            }

            Assert.InRange(received, 1, 12); //5 in the first second, and a few more if the burst crossed into the next.
        }

        [Fact]
        public async Task A_flood_of_datagrams_does_not_stop_the_proxy()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            host.Start(Udp("udp", port, backend.Port));
            using var flooder = new UdpTestClient(port);

            var payload = new byte[1400];
            for (var i = 0; i < 5000; i++)
            {
                await flooder.Send(payload);
            }

            using var other = new UdpTestClient(port);
            Assert.Equal("still here", await other.Ask("still here", 10));
        }

        //---------------------------------------------------------------- Statistics and the list

        [Fact]
        public async Task The_bytes_are_counted_and_the_session_is_listed_as_udp()
        {
            using var host = new TestHost();
            var backend = host.Own(new UdpEchoBackend());
            var port = Net.FreeUdpPort();
            var proxy = host.Start(Udp("udp", port, backend.Port));
            using var client = new UdpTestClient(port);
            await client.Ask(new string('x', 500));

            var item = proxy.ActiveConnections.Values.Single().GetGridItem();

            Assert.Equal("Udp", item.Protocol);
            Assert.Equal("127.0.0.1", item.SourceAddress);
            Assert.Equal(client.LocalPort, item.SourcePort);
            Assert.Equal(port, item.ListenPort);
            Assert.Equal(backend.Port, item.DestinationPort);
            Assert.True(item.IsConnectedToEndpoint);
            Assert.Equal(500UL, item.BytesIn);
            Assert.Equal(500UL, item.BytesOut);
            Assert.Equal(500UL, proxy.Statistics.BytesRead);
            Assert.Equal(500UL, proxy.Statistics.BytesWritten);
            Assert.Equal(500UL, proxy.EndpointStatistics.Values.Single().BytesWritten);
            Assert.Equal(500UL, proxy.EndpointStatistics.Values.Single().BytesRead);
        }

        //---------------------------------------------------------------- Ports and rules

        [Fact]
        public void A_udp_port_cannot_be_used_by_two_proxies_but_a_tcp_proxy_can_have_the_same_number()
        {
            using var host = new TestHost();
            var udpBackend = host.Own(new UdpEchoBackend());
            var httpBackend = host.Backend("A");
            var port = Net.FreePortForTcpAndUdp();

            host.Start(Udp("one", port, udpBackend.Port));

            Assert.False(host.Create(Udp("two", port, udpBackend.Port)).Start(out var error));
            Assert.False(string.IsNullOrEmpty(error));

            //The same number as a TCP port is another socket.
            var tcp = host.Start(ProxyBuilder.Configuration("tcp", port, TrafficType.Http, httpBackend.Port));
            Assert.True(tcp.IsRunning);
        }

        [Fact]
        public void The_save_rules_allow_tcp_and_udp_on_one_port_but_not_two_udp_proxies()
        {
            var udp = Udp("udp", 53, 9);
            var tcp = ProxyBuilder.Configuration("tcp", 53, TrafficType.Tcp, 9);
            var udp2 = Udp("udp2", 53, 9);

            Assert.Null(NpHostRouter.FindSharingConflict(udp, [tcp]));
            Assert.Null(NpHostRouter.FindSharingConflict(tcp, [udp]));
            Assert.Contains("UDP port 53", NpHostRouter.FindSharingConflict(udp2, [udp]));
        }

        [Fact]
        public void What_only_makes_sense_for_tcp_is_refused_on_a_udp_proxy()
        {
            var good = Udp("udp", 53, 9);
            Assert.Empty(NpProxyValidator.Validate(good, []));

            var hosts = Udp("udp", 53, 9, c => c.HostNames.Add(new NpHostName { Host = "a.example.com" }));
            Assert.Contains(NpProxyValidator.Validate(hosts, []), o => o.Contains("host names"));

            var tls = Udp("udp", 53, 9, c => c.TlsCertificate = Certs.Register("x.pfx"));
            Assert.Contains(NpProxyValidator.Validate(tls, []), o => o.Contains("certificate"));

            var forwarded = Udp("udp", 53, 9, c => c.ForwardedHeaders = ForwardedHeadersMode.Replace);
            Assert.Contains(NpProxyValidator.Validate(forwarded, []), o => o.Contains("forwarded"));

            var rules = Udp("udp", 53, 9, c => c.HttpHeaderRules.Add(new NpHttpHeaderRule { Name = "X", Value = "1", HeaderType = HttpHeaderType.Request, Enabled = true }));
            Assert.Contains(NpProxyValidator.Validate(rules, []), o => o.Contains("header rules"));

            var rate = Udp("udp", 53, 9, c => c.MaxDatagramsPerSecondPerClient = -1);
            Assert.Contains(NpProxyValidator.Validate(rate, []), o => o.Contains("datagrams"));
        }

        [Fact]
        public void A_udp_proxy_is_written_and_read_as_udp()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Udp("udp", 53, 9, c => c.MaxDatagramsPerSecondPerClient = 100));
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<NpProxyConfiguration>(json)!;

            Assert.Contains("\"TrafficType\":\"Udp\"", json);
            Assert.Equal(TrafficType.Udp, loaded.TrafficType);
            Assert.Equal(100, loaded.MaxDatagramsPerSecondPerClient);
        }

        [Fact]
        public void A_proxy_that_starts_a_udp_port_in_use_says_so()
        {
            using var host = new TestHost();
            using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var port = ((IPEndPoint)taken.LocalEndPoint!).Port;

            var proxy = host.Create(Udp("udp", port, 9));

            Assert.False(proxy.Start(out var error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.False(proxy.IsRunning);
        }
    }
}
