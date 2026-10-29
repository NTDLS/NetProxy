using NetProxy.PluginApi;
using NetProxy.Plugins.Metrics;
using NetProxy.Plugins.Tests.Support;
using System.Net;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class MetricsTests : PluginTestBase
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

        public override void Dispose()
        {
            _http.Dispose();
            base.Dispose();
        }

        private (MetricsPlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
        {
            settings ??= [];
            settings.TryAdd("Listen", "127.0.0.1:0");
            return Start(new MetricsPlugin(), settings);
        }

        private async Task<string> Scrape(MetricsPlugin plugin, string path = "/metrics", string? token = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{plugin.Port}{path}");
            if (token != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            }

            using var response = await _http.SendAsync(request);
            return $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
        }

        [Fact]
        public async Task The_metrics_are_served_in_the_prometheus_text_format()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Connect(plugin, connection);
            var text = await Scrape(plugin);

            Assert.StartsWith("200 ", text);
            Assert.Contains("# HELP netproxy_connections_total Client connections accepted by a proxy.\n# TYPE netproxy_connections_total counter\nnetproxy_connections_total{proxy=\"web\"} 1\n", text);
            Assert.Contains("netproxy_connections_active{proxy=\"web\"} 1\n", text);
        }

        [Fact]
        public async Task A_connection_is_counted_while_it_is_open_and_when_it_ends()
        {
            var (plugin, _) = Start();
            var first = new FakeConnection();
            var second = new FakeConnection();

            Connect(plugin, first);
            Connect(plugin, second);
            Assert.Equal(2, plugin.Metrics.Get("netproxy_connections_active", "{proxy=\"web\"}"));

            plugin.OnDataSentAsync(first, NpDataDirection.ClientToEndpoint, 300, CancellationToken.None).AsTask().Wait();
            plugin.OnDataSentAsync(first, NpDataDirection.EndpointToClient, 5000, CancellationToken.None).AsTask().Wait();
            plugin.OnDisconnectedAsync(first, new NpDisconnectInfo(NpDisconnectReason.Completed, TimeSpan.FromSeconds(2), 300, 5000), CancellationToken.None).AsTask().Wait();
            plugin.OnDisconnectedAsync(first, new NpDisconnectInfo(NpDisconnectReason.Completed, TimeSpan.FromSeconds(2), 300, 5000), CancellationToken.None).AsTask().Wait(); //Once only.

            var text = await Scrape(plugin);
            Assert.Contains("netproxy_connections_active{proxy=\"web\"} 1\n", text);
            Assert.Contains("netproxy_bytes_total{proxy=\"web\",direction=\"in\"} 300\n", text);
            Assert.Contains("netproxy_bytes_total{proxy=\"web\",direction=\"out\"} 5000\n", text);
            Assert.Contains("netproxy_disconnects_total{proxy=\"web\",reason=\"Completed\"} 1\n", text);
            Assert.Contains("netproxy_connection_duration_seconds_bucket{proxy=\"web\",le=\"1\"} 0\n", text);
            Assert.Contains("netproxy_connection_duration_seconds_bucket{proxy=\"web\",le=\"5\"} 1\n", text);
            Assert.Contains("netproxy_connection_duration_seconds_bucket{proxy=\"web\",le=\"+Inf\"} 1\n", text);
            Assert.Contains("netproxy_connection_duration_seconds_sum{proxy=\"web\"} 2\n", text);
            Assert.Contains("netproxy_connection_duration_seconds_count{proxy=\"web\"} 1\n", text);
        }

        [Fact]
        public async Task Endpoints_that_fail_and_proxies_that_are_up_or_down_are_counted()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            var endpoint = new NpEndpointInfo(Guid.NewGuid(), "10.0.0.5", 8080, false);
            var proxy = new NpProxyInfo(Guid.NewGuid(), "web", 443, NpTrafficKind.Https);

            plugin.OnEndpointConnectFailedAsync(connection, endpoint, "refused", CancellationToken.None).AsTask().Wait();
            plugin.OnEndpointConnectFailedAsync(connection, endpoint, "refused", CancellationToken.None).AsTask().Wait();
            plugin.OnEndpointsUnavailableAsync(new NpUnavailableEventArgs(connection, [], true), CancellationToken.None).AsTask().Wait();
            plugin.OnConnectionRefusedAsync(proxy, "1.2.3.4", "limit", CancellationToken.None).AsTask().Wait();
            plugin.OnProxyStartedAsync(proxy, ["0.0.0.0:443"], CancellationToken.None).AsTask().Wait();
            plugin.OnErrorAsync(new NpPluginError("x", null, null, null), CancellationToken.None).AsTask().Wait();
            plugin.OnManagementLoginAsync(new NpLoginInfo("a", false), CancellationToken.None).AsTask().Wait();
            plugin.OnManagementLoginAsync(new NpLoginInfo("a", true), CancellationToken.None).AsTask().Wait();

            var text = await Scrape(plugin);
            Assert.Contains("netproxy_endpoint_connect_failures_total{proxy=\"web\",endpoint=\"10.0.0.5:8080\"} 2\n", text);
            Assert.Contains("netproxy_endpoints_unavailable_total{proxy=\"web\"} 1\n", text);
            Assert.Contains("netproxy_connections_refused_total{proxy=\"web\"} 1\n", text);
            Assert.Contains("netproxy_proxy_up{proxy=\"web\",traffic=\"Https\"} 1\n", text);
            Assert.Contains("netproxy_plugin_errors_total 1\n", text);
            Assert.Contains("netproxy_management_logins_total{result=\"failure\"} 1\n", text);
            Assert.Contains("netproxy_management_logins_total{result=\"success\"} 1\n", text);

            plugin.OnProxyStoppedAsync(proxy, CancellationToken.None).AsTask().Wait();
            Assert.Contains("netproxy_proxy_up{proxy=\"web\",traffic=\"Https\"} 0\n", await Scrape(plugin));
        }

        [Fact]
        public async Task Http_requests_are_counted_by_status_class_with_how_long_they_took()
        {
            var (plugin, host) = Start();
            var connection = new FakeConnection();
            Connect(plugin, connection);

            Send(plugin, connection, Http.Request("/a").Concat(Http.Request("/b")).Concat(Http.Request("/c")).ToArray());
            Send(plugin, connection, Http.Bytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\nHTTP/1.1 404 No\r\nContent-Length: 0\r\n\r\nHTTP/1.1 503 No\r\nContent-Length: 0\r\n\r\n"),
                direction: NpDataDirection.EndpointToClient);

            var text = await Scrape(plugin);
            Assert.Contains("netproxy_http_requests_total{proxy=\"web\",code=\"2xx\"} 1\n", text);
            Assert.Contains("netproxy_http_requests_total{proxy=\"web\",code=\"4xx\"} 1\n", text);
            Assert.Contains("netproxy_http_requests_total{proxy=\"web\",code=\"5xx\"} 1\n", text);
            Assert.Contains("netproxy_http_request_duration_seconds_count{proxy=\"web\"} 3\n", text);

            Send(plugin, new FakeConnection { IsHttp2 = true }, Http.Request("/h2"));
            Assert.Empty(host.Texts.Where(o => o.Contains("HTTP/2")).Skip(1));
        }

        [Fact]
        public async Task Http_metrics_can_be_switched_off()
        {
            var (plugin, _) = Start(new() { ["HttpMetrics"] = "false" });
            var connection = new FakeConnection();
            Connect(plugin, connection);

            Send(plugin, connection, Http.Request("/a"));
            Send(plugin, connection, Http.Bytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"), direction: NpDataDirection.EndpointToClient);

            Assert.DoesNotContain("netproxy_http_requests_total", await Scrape(plugin));
        }

        [Fact]
        public async Task Label_values_cannot_break_the_format()
        {
            var (plugin, _) = Start();
            var evil = new FakeConnection { Proxy = new(Guid.NewGuid(), "we\"b\nx\\", 443, NpTrafficKind.Https) };

            Connect(plugin, evil);

            Assert.Contains("netproxy_connections_total{proxy=\"we\\\"b\\nx\\\\\"} 1\n", await Scrape(plugin));
        }

        [Fact]
        public async Task A_token_is_asked_for_when_one_is_set_and_other_addresses_are_not_found()
        {
            var (plugin, _) = Start(new() { ["Token"] = "s3cret" });

            Assert.StartsWith("401 ", await Scrape(plugin));
            Assert.StartsWith("401 ", await Scrape(plugin, token: "wrong"));
            Assert.StartsWith("200 ", await Scrape(plugin, token: "s3cret"));
            Assert.StartsWith("404 ", await Scrape(plugin, "/other", "s3cret"));
        }

        [Fact]
        public async Task The_address_can_be_changed_and_only_get_is_answered()
        {
            var (plugin, _) = Start(new() { ["Path"] = "/stats" });

            Assert.StartsWith("200 ", await Scrape(plugin, "/stats"));
            Assert.StartsWith("200 ", await Scrape(plugin, "/stats?x=1"));
            Assert.StartsWith("404 ", await Scrape(plugin));

            using var post = await _http.PostAsync($"http://127.0.0.1:{plugin.Port}/stats", new StringContent("x"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        }

        [Theory]
        [InlineData("127.0.0.1:9464", "127.0.0.1", 9464)]
        [InlineData("0.0.0.0:80", "0.0.0.0", 80)]
        [InlineData("*:9100", "0.0.0.0", 9100)]
        [InlineData("[::1]:9000", "::1", 9000)]
        [InlineData("9200", "127.0.0.1", 9200)]
        public void The_listen_address_is_read(string text, string address, int port)
        {
            var endpoint = MetricsPlugin.ParseListen(text, out var problem);

            Assert.Null(problem);
            Assert.Equal(IPAddress.Parse(address), endpoint.Address);
            Assert.Equal(port, endpoint.Port);
        }

        [Fact]
        public void A_listen_address_that_is_wrong_is_reported_and_replaced()
        {
            var endpoint = MetricsPlugin.ParseListen("nonsense", out var problem);

            Assert.NotNull(problem);
            Assert.Equal(new IPEndPoint(IPAddress.Loopback, 9464), endpoint);
        }

        [Fact]
        public void A_port_that_is_taken_is_reported_and_does_not_stop_the_plugin()
        {
            var (first, _) = Start();
            var (second, host) = Start(new() { ["Listen"] = $"127.0.0.1:{first.Port}" });

            Assert.Equal(0, second.Port);
            Assert.Contains(host.Messages, o => o.Level == NpPluginLogLevel.Error && o.Message.Contains("could not listen"));
            Connect(second, new FakeConnection()); //Still counts.
        }

        [Fact]
        public void The_registry_writes_histograms_with_cumulative_buckets()
        {
            var registry = new MetricsRegistry();
            registry.Declare("t", "histogram", "A test.");
            double[] bounds = [1, 10];

            registry.Observe("t", string.Empty, 0.5, bounds);
            registry.Observe("t", string.Empty, 5, bounds);
            registry.Observe("t", string.Empty, 50, bounds);

            Assert.Equal("# HELP t A test.\n# TYPE t histogram\nt_bucket{le=\"1\"} 1\nt_bucket{le=\"10\"} 2\nt_bucket{le=\"+Inf\"} 3\nt_sum 55.5\nt_count 3\n", registry.Render());
        }
    }
}
