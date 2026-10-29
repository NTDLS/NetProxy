using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Net;
using static NetProxy.Plugins.Metrics.MetricsRegistry;

namespace NetProxy.Plugins.Metrics
{
    /// <summary>
    /// Publishes what NetProxy is doing as metrics that Prometheus (or anything that reads its text format) collects from
    /// http://host:9464/metrics: connections, bytes, how long connections last, why they ended, endpoints that could not be connected to,
    /// whether each proxy is up, and for HTTP/1.x traffic the requests by status and how long they took.
    ///
    /// The metrics are served by a small web server of its own. It listens on 127.0.0.1 unless Listen says otherwise (Listen=0.0.0.0:9464
    /// for every address: set a Token then, which a collector sends as a bearer token). Counters start at zero when the service starts.
    /// </summary>
    [NpPlugin("Metrics", Version = "1.0", Author = "NetworkDLS",
        Description = "Publishes connection, traffic and HTTP metrics in the Prometheus text format.")]
    public sealed class MetricsPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.Metrics";
        private static readonly double[] DurationBounds = [0.1, 0.5, 1, 5, 30, 120, 600, 3600];
        private static readonly double[] RequestBounds = [0.005, 0.025, 0.1, 0.25, 0.5, 1, 2.5, 10];

        private sealed class ConnectionMark(string proxy)
        {
            public string Proxy { get; } = proxy;
            public ExchangeTracker? Tracker { get; set; }
        }

        private readonly MetricsRegistry _metrics = new();
        private MetricsServer? _server;
        private bool _http = true;
        private int _warnedHttp2;

        internal MetricsRegistry Metrics => _metrics;
        internal int Port => _server?.Port ?? 0;

        public MetricsPlugin()
        {
            _metrics.Declare("netproxy_connections_total", "counter", "Client connections accepted by a proxy.");
            _metrics.Declare("netproxy_connections_active", "gauge", "Client connections that are open now.");
            _metrics.Declare("netproxy_connections_refused_total", "counter", "Connections turned away because a proxy was at one of its limits.");
            _metrics.Declare("netproxy_disconnects_total", "counter", "Connections that ended, by why.");
            _metrics.Declare("netproxy_connection_duration_seconds", "histogram", "How long connections lasted.");
            _metrics.Declare("netproxy_bytes_total", "counter", "Bytes passed on: direction=in from clients to endpoints, out from endpoints to clients.");
            _metrics.Declare("netproxy_endpoint_connect_failures_total", "counter", "Times an endpoint could not be connected to.");
            _metrics.Declare("netproxy_endpoints_unavailable_total", "counter", "Clients that could not be connected to any endpoint.");
            _metrics.Declare("netproxy_proxy_up", "gauge", "1 when a proxy is listening, 0 when it is stopped or could not start.");
            _metrics.Declare("netproxy_http_requests_total", "counter", "HTTP/1.x requests answered, by the class of the status (2xx, 3xx, ...).");
            _metrics.Declare("netproxy_http_request_duration_seconds", "histogram", "How long HTTP/1.x requests took to be answered.");
            _metrics.Declare("netproxy_plugin_errors_total", "counter", "Failures that the service reported to plug-ins.");
            _metrics.Declare("netproxy_management_logins_total", "counter", "Logins to the management interface, by result.");
        }

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _http = settings.Flag("HttpMetrics", true);
            var path = settings.Text("Path", "/metrics");
            if (path.StartsWith('/') == false)
            {
                settings.Problems.Add($"The setting Path must start with a /, so /metrics is used instead of '{path}'.");
                path = "/metrics";
            }

            var endpoint = ParseListen(settings.Text("Listen", "127.0.0.1:9464"), out var problem);
            if (problem != null)
            {
                settings.Problems.Add(problem);
            }

            var token = settings.Text("Token");
            foreach (var text in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, text);
            }

            try
            {
                _server = new MetricsServer(endpoint, path, token, _metrics.Render, text => Host.Log(NpPluginLogLevel.Warning, text));
                _server.Start();

                Host.Log(NpPluginLogLevel.Information, $"Metrics are at http://{(endpoint.Address.Equals(IPAddress.Any) ? "<this machine>" : endpoint.Address.ToString())}:{_server.Port}{path}"
                    + (token != null ? " (with the token)" : endpoint.Address.Equals(IPAddress.Loopback) ? string.Empty : ". There is no Token, so anyone who can reach that address can read them."));
            }
            catch (Exception ex)
            {
                _server?.Dispose();
                _server = null;
                Host.Log(NpPluginLogLevel.Error, $"The metrics server could not listen on {endpoint}: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            _server?.Dispose();
            _server = null;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// "127.0.0.1:9464", "[::1]:9464", "0.0.0.0:9464", "*:9464" (every address), or just a port.
        /// </summary>
        internal static IPEndPoint ParseListen(string text, out string? problem)
        {
            problem = null;
            var fallback = new IPEndPoint(IPAddress.Loopback, 9464);

            if (int.TryParse(text, out var onlyPort) && onlyPort is >= 0 and <= 65535)
            {
                return new IPEndPoint(IPAddress.Loopback, onlyPort);
            }

            var colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text[(colon + 1)..], out var port) && port is >= 0 and <= 65535)
            {
                var host = text[..colon].Trim('[', ']');
                if (host == "*")
                {
                    return new IPEndPoint(IPAddress.Any, port);
                }

                if (IPAddress.TryParse(host, out var address))
                {
                    return new IPEndPoint(address, port);
                }
            }

            problem = $"The setting Listen must be an address and a port (127.0.0.1:9464, 0.0.0.0:9464), not '{text}', so 127.0.0.1:9464 is used.";
            return fallback;
        }

        //---------------------------------------------------------------- Proxies

        public override ValueTask OnProxyStartedAsync(NpProxyInfo proxy, IReadOnlyList<string> listeningOn, CancellationToken cancellationToken)
        {
            _metrics.Set("netproxy_proxy_up", Labels(("proxy", proxy.Name), ("traffic", proxy.Traffic.ToString())), 1);
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnProxyStartFailedAsync(NpProxyInfo proxy, string error, CancellationToken cancellationToken)
        {
            _metrics.Set("netproxy_proxy_up", Labels(("proxy", proxy.Name), ("traffic", proxy.Traffic.ToString())), 0);
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnProxyStoppedAsync(NpProxyInfo proxy, CancellationToken cancellationToken)
        {
            _metrics.Set("netproxy_proxy_up", Labels(("proxy", proxy.Name), ("traffic", proxy.Traffic.ToString())), 0);
            return ValueTask.CompletedTask;
        }

        //---------------------------------------------------------------- Connections

        public override ValueTask OnConnectionRefusedAsync(NpProxyInfo proxy, string clientAddress, string reason, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_connections_refused_total", Labels(("proxy", proxy.Name)));
            return ValueTask.CompletedTask;
        }

        public override ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
        {
            var labels = Labels(("proxy", connection.Proxy.Name));
            _metrics.Add("netproxy_connections_total", labels);
            _metrics.Add("netproxy_connections_active", labels);
            connection.State[StateKey] = new ConnectionMark(connection.Proxy.Name);
            return ValueTask.FromResult(NpVerdict.Allow);
        }

        public override ValueTask OnEndpointConnectFailedAsync(INpConnection connection, NpEndpointInfo endpoint, string error, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_endpoint_connect_failures_total", Labels(("proxy", connection.Proxy.Name), ("endpoint", $"{endpoint.Address}:{endpoint.Port}")));
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnEndpointsUnavailableAsync(NpUnavailableEventArgs unavailable, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_endpoints_unavailable_total", Labels(("proxy", unavailable.Connection.Proxy.Name)));
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataSentAsync(INpConnection connection, NpDataDirection direction, int byteCount, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_bytes_total", Labels(("proxy", connection.Proxy.Name), ("direction", direction == NpDataDirection.ClientToEndpoint ? "in" : "out")), byteCount);
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (_http == false || connection.Proxy.Traffic is not (NpTrafficKind.Http or NpTrafficKind.Https)
                    || connection.State.TryGetValue(StateKey, out var existing) == false || existing is not ConnectionMark mark)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning, "A client uses HTTP/2, which is compressed, so its requests are not counted (its connections and bytes are). Untick Allow HTTP/2 on the proxy to have them counted.");
                    }

                    return ValueTask.CompletedTask;
                }

                mark.Tracker ??= new ExchangeTracker(connection, _ => string.Empty, Record);
                mark.Tracker.Feed(data.Direction, data.Data.Span);
            }
            catch (Exception ex)
            {
                //Counting is never allowed to get in the way of the traffic.
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be counted: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        private void Record(HttpExchange exchange)
        {
            if (exchange.Status == 0)
            {
                return; //Never answered.
            }

            var proxy = ("proxy", exchange.Proxy);
            _metrics.Add("netproxy_http_requests_total", Labels(proxy, ("code", $"{exchange.Status / 100}xx")));
            _metrics.Observe("netproxy_http_request_duration_seconds", Labels(proxy), exchange.DurationMs / 1000.0, RequestBounds);
        }

        public override ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
        {
            if (connection.State.Remove(StateKey))
            {
                var proxy = ("proxy", connection.Proxy.Name);
                _metrics.Add("netproxy_connections_active", Labels(proxy), -1);
                _metrics.Add("netproxy_disconnects_total", Labels(proxy, ("reason", info.Reason.ToString())));
                _metrics.Observe("netproxy_connection_duration_seconds", Labels(proxy), info.Duration.TotalSeconds, DurationBounds);
            }

            return ValueTask.CompletedTask;
        }

        //---------------------------------------------------------------- The service

        public override ValueTask OnErrorAsync(NpPluginError error, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_plugin_errors_total", string.Empty);
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnManagementLoginAsync(NpLoginInfo login, CancellationToken cancellationToken)
        {
            _metrics.Add("netproxy_management_logins_total", Labels(("result", login.Succeeded ? "success" : "failure")));
            return ValueTask.CompletedTask;
        }
    }
}
