using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    /// <summary>
    /// What a proxy does with a connection: where it sends it, how much it lets in, and what it does to the bytes.
    /// </summary>
    public class ProxyBehaviorTests
    {
        //---------------------------------------------------------------- TCP traffic

        [Fact]
        public async Task TCP_traffic_goes_both_ways_unchanged()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("tcp", port, TrafficType.Tcp, echo.Port));

            var sent = RandomNumberGenerator.GetBytes(3 * 1024 * 1024 + 17);
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            var writing = Task.Run(async () => { await stream.WriteAsync(sent); });
            var received = new byte[sent.Length];
            var offset = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (offset < received.Length)
            {
                var read = await stream.ReadAsync(received.AsMemory(offset), timeout.Token);
                Assert.True(read > 0, "The connection ended before everything came back.");
                offset += read;
            }

            await writing;
            Assert.True(sent.AsSpan().SequenceEqual(received));
        }

        [Fact]
        public async Task A_client_that_is_done_sending_still_gets_the_rest_of_the_response()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("tcp", port, TrafficType.Tcp, echo.Port));

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();

            await stream.WriteAsync("goodbye"u8.ToArray());
            client.Client.Shutdown(SocketShutdown.Send);

            var text = new StringBuilder();
            var buffer = new byte[100];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                text.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            Assert.Equal("goodbye", text.ToString());
        }

        [Fact]
        public async Task Many_connections_at_once_each_get_their_own_data()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("tcp", port, TrafficType.Tcp, echo.Port));

            var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(async i =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port);
                var stream = client.GetStream();
                var message = Encoding.ASCII.GetBytes($"connection-{i}-{new string('x', i * 10)}");
                await stream.WriteAsync(message);

                var received = new byte[message.Length];
                var offset = 0;
                while (offset < received.Length)
                {
                    var read = await stream.ReadAsync(received.AsMemory(offset));
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                }

                return Encoding.ASCII.GetString(received, 0, offset) == Encoding.ASCII.GetString(message);
            }));

            Assert.All(results, Assert.True);
        }

        //---------------------------------------------------------------- Choosing an endpoint

        private static async Task<string> BackendOf(int port)
        {
            var response = await Client.Http(port);
            var start = response.IndexOf("backend=", StringComparison.Ordinal);
            return start < 0 ? "none" : response[(start + 8)..].Split(' ')[0];
        }

        [Fact]
        public async Task Fail_over_uses_the_first_endpoint_that_works()
        {
            using var host = new TestHost();
            var down = Net.FreePort(); //Nothing listens here.
            var b = host.Backend("B");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, down, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            Assert.Equal("B", await BackendOf(port));
            Assert.Equal("B", await BackendOf(port));
        }

        [Fact]
        public async Task Fail_over_prefers_the_first_endpoint_for_as_long_as_it_works()
        {
            using var host = new TestHost();
            var a = host.Backend("A");
            var b = host.Backend("B");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            for (var i = 0; i < 4; i++)
            {
                Assert.Equal("A", await BackendOf(port));
            }
        }

        [Fact]
        public async Task Round_robin_takes_the_endpoints_in_turn()
        {
            using var host = new TestHost();
            var a = host.Backend("A");
            var b = host.Backend("B");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.RoundRobbin;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            var seen = new List<string>();
            for (var i = 0; i < 6; i++)
            {
                seen.Add(await BackendOf(port));
            }

            Assert.Equal(3, seen.Count(o => o == "A"));
            Assert.Equal(3, seen.Count(o => o == "B"));
            Assert.All(seen.Zip(seen.Skip(1)), pair => Assert.NotEqual(pair.First, pair.Second)); //Alternating.
        }

        [Fact]
        public async Task A_proxy_written_by_hand_with_no_enabled_flags_starts_and_serves()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();

            var configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<NpProxyConfiguration>($$"""
                {
                  "Name": "by-hand", "ListenPort": {{port}}, "TrafficType": "Http", "AutoStart": true,
                  "Bindings": [ { "Address": "127.0.0.1" } ],
                  "Endpoints": { "Collection": [ { "Address": "127.0.0.1", "Port": {{backend.Port}}, "TrafficType": "Http" } ] }
                }
                """)!;
            host.Start(configuration);

            Assert.Contains("backend=A", await Client.Http(port));
        }

        [Fact]
        public async Task A_disabled_endpoint_is_not_used()
        {
            using var host = new TestHost();
            var a = host.Backend("A");
            var b = host.Backend("B");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.RoundRobbin;
                c.Endpoints.Collection[0].Enabled = false;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            for (var i = 0; i < 4; i++)
            {
                Assert.Equal("B", await BackendOf(port));
            }
        }

        [Fact]
        public async Task Sticky_sessions_keep_a_client_on_the_endpoint_it_had()
        {
            using var host = new TestHost();
            var a = host.Backend("A");
            var b = host.Backend("B");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, a.Port, c =>
            {
                c.Endpoints.ConnectionPattern = ConnectionPattern.RoundRobbin;
                c.UseStickySessions = true;
                c.Endpoints.Add(ProxyBuilder.Endpoint(b.Port));
            }));

            var first = await BackendOf(port);
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(first, await BackendOf(port));
            }
        }

        [Fact]
        public async Task When_no_endpoint_can_be_reached_the_client_is_disconnected()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, Net.FreePort()));

            Assert.Equal("", await Client.Http(port));
        }

        [Fact]
        public async Task An_endpoint_that_speaks_tls_is_reached_with_tls()
        {
            using var host = new TestHost();
            var tls = host.Own(new TlsEchoBackend(Certs.PfxBytes("backend.test.local")));
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, tls.Port, c =>
            {
                c.Endpoints.Collection[0].TrafficType = TrafficType.Https;
                c.Endpoints.Collection[0].TlsValidateSSLCertificate = false;
            }));

            Assert.Contains("hello from tls", await Client.Http(port));
        }

        [Fact]
        public async Task An_endpoint_certificate_that_is_not_trusted_is_refused_when_validation_is_on()
        {
            using var host = new TestHost();
            var tls = host.Own(new TlsEchoBackend(Certs.PfxBytes("backend.test.local")));
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, tls.Port, c =>
            {
                c.Endpoints.Collection[0].TrafficType = TrafficType.Https;
                c.Endpoints.Collection[0].TlsValidateSSLCertificate = true;
            }));

            Assert.DoesNotContain("hello from tls", await Client.Http(port));
        }

        [Fact]
        public async Task An_https_proxy_in_front_of_a_tls_endpoint_encrypts_both_sides_separately()
        {
            using var host = new TestHost();
            var tls = host.Own(new TlsEchoBackend(Certs.PfxBytes("backend.test.local")));
            var port = Net.FreePort();
            var frontCertificate = Certs.WritePfx(host.Directory, "front.test.local");
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Https, tls.Port, c =>
            {
                c.TlsCertificate = Certs.Register(frontCertificate);
                c.Endpoints.Collection[0].TrafficType = TrafficType.Https;
                c.Endpoints.Collection[0].TlsValidateSSLCertificate = false;
            }));

            var (body, certificate) = await Client.Https(port, "front.test.local");

            Assert.Contains("hello from tls", body);
            Assert.Equal("front.test.local", certificate);
        }

        //---------------------------------------------------------------- Limits

        private static async Task<bool> IsClosedByTheProxy(TcpClient client)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                return await client.GetStream().ReadAsync(new byte[1], timeout.Token) == 0;
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        [Fact]
        public async Task The_most_connections_a_client_may_have_is_enforced_and_given_back()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port, c => c.MaxConnectionsPerClient = 2));

            using var first = new TcpClient(); await first.ConnectAsync("127.0.0.1", port);
            using var second = new TcpClient(); await second.ConnectAsync("127.0.0.1", port);
            await Task.Delay(300);

            using var third = new TcpClient(); await third.ConnectAsync("127.0.0.1", port);
            Assert.True(await IsClosedByTheProxy(third), "The third connection should have been closed.");

            first.Dispose();
            await Task.Delay(500);

            using var fourth = new TcpClient(); await fourth.ConnectAsync("127.0.0.1", port);
            await fourth.GetStream().WriteAsync("x"u8.ToArray());
            var buffer = new byte[1];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Assert.Equal(1, await fourth.GetStream().ReadAsync(buffer, timeout.Token));
        }

        [Fact]
        public async Task The_most_connections_the_proxy_may_have_is_enforced()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port, c => c.MaxConnections = 1));

            using var first = new TcpClient(); await first.ConnectAsync("127.0.0.1", port);
            await Task.Delay(300);

            using var second = new TcpClient(); await second.ConnectAsync("127.0.0.1", port);
            Assert.True(await IsClosedByTheProxy(second));
        }

        [Fact]
        public async Task A_connection_with_no_traffic_is_closed_after_the_idle_timeout()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port, c => c.IdleTimeoutSeconds = 1));

            using var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port);
            Assert.True(await IsClosedByTheProxy(client));
        }

        [Fact]
        public async Task A_connection_with_traffic_is_not_closed_by_the_idle_timeout()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port, c => c.IdleTimeoutSeconds = 2));

            using var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            var buffer = new byte[1];

            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(700);
                await stream.WriteAsync("x"u8.ToArray());
                Assert.Equal(1, await stream.ReadAsync(buffer));
            }
        }

        //---------------------------------------------------------------- Lifecycle and statistics

        [Fact]
        public void A_proxy_without_an_enabled_endpoint_does_not_start()
        {
            using var host = new TestHost();
            var configuration = ProxyBuilder.Configuration("p", Net.FreePort(), TrafficType.Http, 9000);
            configuration.Endpoints.Collection[0].Enabled = false;

            var proxy = host.Create(configuration);

            Assert.False(proxy.Start(out var error));
            Assert.Contains("endpoint", error);
            Assert.False(proxy.IsRunning);
        }

        [Fact]
        public void A_proxy_without_an_enabled_binding_does_not_start()
        {
            using var host = new TestHost();
            var configuration = ProxyBuilder.Configuration("p", Net.FreePort(), TrafficType.Http, 9000);
            configuration.Bindings[0].Enabled = false;

            Assert.False(host.Create(configuration).Start(out var error));
            Assert.Contains("binding", error);
        }

        [Fact]
        public async Task Starting_twice_is_harmless_and_a_stopped_proxy_refuses_connections_and_starts_again()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            var proxy = host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, backend.Port));

            Assert.True(proxy.Start(out _));
            Assert.True(proxy.IsRunning);
            Assert.Contains("backend=A", await Client.Http(port));

            proxy.Stop();
            Assert.False(proxy.IsRunning);
            await Assert.ThrowsAnyAsync<SocketException>(async () => { using var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port); });

            Assert.True(proxy.Start(out var error), error);
            Assert.Contains("backend=A", await Client.Http(port));
        }

        [Fact]
        public async Task Stopping_a_proxy_closes_its_open_connections()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            var proxy = host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port));

            using var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port);
            await client.GetStream().WriteAsync("x"u8.ToArray());
            await Client.ReadExactly(client.GetStream(), 1);

            proxy.Stop();

            Assert.True(await IsClosedByTheProxy(client));
            Assert.Empty(proxy.ActiveConnections);
        }

        [Fact]
        public async Task Statistics_count_connections_and_bytes()
        {
            using var host = new TestHost();
            var echo = host.Own(new TcpEchoBackend());
            var port = Net.FreePort();
            var proxy = host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Tcp, echo.Port));

            using (var client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port);
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[1000]);

                var received = 0;
                var buffer = new byte[1000];
                while (received < 1000)
                {
                    received += await stream.ReadAsync(buffer.AsMemory(0, 1000 - received));
                }

                Assert.Equal(1, proxy.Statistics.CurrentConnections);
            }

            await Client.Eventually(() => Task.FromResult(proxy.Statistics.CurrentConnections), o => o == 0, seconds: 5);

            Assert.Equal(0, proxy.Statistics.CurrentConnections);
            Assert.Equal(1UL, proxy.Statistics.TotalConnections);
            Assert.Equal(1000UL, proxy.Statistics.BytesRead);
            Assert.Equal(1000UL, proxy.Statistics.BytesWritten);
        }

        //---------------------------------------------------------------- Header rules and forwarded headers

        [Fact]
        public async Task Header_rules_change_the_request_the_endpoint_gets()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, backend.Port, c =>
                c.HttpHeaderRules.Add(new NpHttpHeaderRule { Name = "Host", Value = "internal.local", Action = HttpHeaderAction.Update, HeaderType = HttpHeaderType.Request, Enabled = true })));

            Assert.Contains("host=[internal.local]", await Client.Http(port, "public.example.com"));
        }

        [Fact]
        public async Task Header_rules_also_apply_to_the_decrypted_requests_of_an_https_proxy()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Https, backend.Port, c =>
            {
                c.TlsCertificate = Certs.Register(Certs.WritePfx(host.Directory, "p.test.local"));
                c.HttpHeaderRules.Add(new NpHttpHeaderRule { Name = "Host", Value = "internal.local", Action = HttpHeaderAction.Update, HeaderType = HttpHeaderType.Request, Enabled = true });
            }));

            var (body, _) = await Client.Https(port, "p.test.local", host: "public.example.com");
            Assert.Contains("host=[internal.local]", body);
        }

        [Fact]
        public async Task Forwarded_headers_are_off_unless_asked_for()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, backend.Port));

            var response = await Client.Http(port, extraHeaders: "X-Forwarded-For: 6.6.6.6\r\n");
            Assert.Contains("xff=[6.6.6.6]", response);
            Assert.Contains("proto=[]", response);
        }

        [Fact]
        public async Task Set_replaces_what_the_client_sent_and_applies_to_every_request_on_the_connection()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, backend.Port, c => c.ForwardedHeaders = ForwardedHeadersMode.Replace));

            var response = await Client.Http(port, extraHeaders: "X-Forwarded-For: 6.6.6.6\r\nX-Forwarded-Proto: https\r\n");
            Assert.Contains("xff=[127.0.0.1] proto=[http]", response);

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            var stream = client.GetStream();
            var text = new StringBuilder();
            var buffer = new byte[4096];

            for (var i = 0; i < 3; i++)
            {
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET /{i} HTTP/1.1\r\nHost: x\r\nX-Forwarded-For: 6.6.6.6\r\n\r\n"));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var before = text.ToString().Split(" end").Length;
                while (text.ToString().Split(" end").Length == before)
                {
                    text.Append(Encoding.ASCII.GetString(buffer, 0, await stream.ReadAsync(buffer, timeout.Token)));
                }
            }

            Assert.Equal(3, text.ToString().Split("xff=[127.0.0.1]").Length - 1);
            Assert.DoesNotContain("6.6.6.6", text.ToString());
        }

        [Fact]
        public async Task Append_extends_the_chain_the_client_sent()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Http, backend.Port, c => c.ForwardedHeaders = ForwardedHeadersMode.Append));

            var response = await Client.Http(port, extraHeaders: "X-Forwarded-For: 10.0.0.9\r\nX-Forwarded-Proto: https\r\n");

            Assert.Contains("xff=[10.0.0.9, 127.0.0.1] proto=[https]", response);
        }

        [Fact]
        public async Task An_https_proxy_tells_the_endpoint_the_client_used_https()
        {
            using var host = new TestHost();
            var backend = host.Backend("A");
            var port = Net.FreePort();
            host.Start(ProxyBuilder.Configuration("p", port, TrafficType.Https, backend.Port, c =>
            {
                c.TlsCertificate = Certs.Register(Certs.WritePfx(host.Directory, "p.test.local"));
                c.ForwardedHeaders = ForwardedHeadersMode.Replace;
            }));

            var (body, _) = await Client.Https(port, "p.test.local", extraHeaders: "X-Forwarded-For: 6.6.6.6\r\n");

            Assert.Contains("xff=[127.0.0.1] proto=[https]", body);
        }
    }
}
