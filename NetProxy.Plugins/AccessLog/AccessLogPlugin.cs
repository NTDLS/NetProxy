using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Net;

namespace NetProxy.Plugins.AccessLog
{
    /// <summary>
    /// Writes a web server style access log of the HTTP and HTTPS traffic: one line per request with the client, the request, the status
    /// of the response and its size. Format is common, combined (the default, with the referrer and the user agent) or json. A file per
    /// day, in the folder Directory (default: the data folder of the plug-in, under logs), and files older than KeepDays are deleted.
    ///
    /// It only looks. The data is passed on untouched, a request is followed for as long as it is HTTP/1.x (HTTP/2 is compressed, and is
    /// not followed), and nothing here waits for the disk. Behind Cloudflare or another proxy set TrustedProxies, and the line has the
    /// client and not the proxy.
    /// </summary>
    [NpPlugin("Access Log", Version = "1.0", Author = "NetworkDLS",
        Description = "Writes an access log (common, combined or JSON) of the web traffic of a proxy, with a file per day.")]
    public sealed class AccessLogPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.AccessLog";

        private readonly TimeProvider _clock;
        private LogWriter? _writer;
        private TrustedProxies _trusted = new([], [], "TrustedProxies");
        private AccessLogFormat _format = AccessLogFormat.Combined;
        private List<string> _ignorePaths = [];
        private List<string> _ignoreClients = [];
        private bool _includeTcp;
        private int _warnedHttp2;

        public AccessLogPlugin() : this(TimeProvider.System)
        {
        }

        internal AccessLogPlugin(TimeProvider clock) => _clock = clock;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            var format = settings.Text("Format", "combined");
            if (Enum.TryParse<AccessLogFormat>(format, ignoreCase: true, out var parsed))
            {
                _format = parsed;
            }
            else
            {
                settings.Problems.Add($"The setting Format must be common, combined or json; '{format}' is not, so combined is used.");
            }

            _trusted = new TrustedProxies(settings.List("TrustedProxies"), settings.Problems, "TrustedProxies");
            _ignorePaths = settings.List("IgnorePathPrefixes");
            _ignoreClients = settings.List("IgnoreClients");
            _includeTcp = settings.Flag("IncludeTcpProxies", false);

            var directory = Path.GetFullPath(settings.Text("Directory", "logs"), Host.DataDirectory);
            var pattern = settings.Text("FileName", "access-{date}.log");
            if (pattern.Contains("{date}") == false || pattern.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                settings.Problems.Add("The setting FileName must be a file name with {date} in it (access-{date}.log), so that is used.");
                pattern = "access-{date}.log";
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _writer = new LogWriter(directory, pattern, settings.Number("MaxFileMB", 0, 0, 100_000) * 1024L * 1024L, settings.Number("KeepDays", 30, 0, 36_500),
                _clock, text => Host.Log(NpPluginLogLevel.Warning, text));

            try
            {
                _writer.Start();
                Host.Log(NpPluginLogLevel.Information, $"Writing the {_format.ToString().ToLowerInvariant()} access log to {directory}.");
            }
            catch (Exception ex)
            {
                _writer = null;
                Host.Log(NpPluginLogLevel.Error, $"The folder of the access log ('{directory}') could not be used, so nothing is logged: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            _writer?.Stop(TimeSpan.FromSeconds(3));
            _writer = null;
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (_writer == null || IsLogged(connection) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning,
                            "A client uses HTTP/2, which is compressed and is not logged. Untick Allow HTTP/2 on the proxy to have those requests logged.");
                    }

                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    existing = new ExchangeTracker(connection, head => Client(connection, head), Write);
                    connection.State[StateKey] = existing;
                }

                ((ExchangeTracker)existing!).Feed(data.Direction, data.Data.Span);
            }
            catch (Exception ex)
            {
                //Logging is never allowed to get in the way of the traffic.
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be logged: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
        {
            try
            {
                if (connection.State.TryGetValue(StateKey, out var existing) && existing is ExchangeTracker tracker)
                {
                    tracker.Finish();
                    connection.State.Remove(StateKey);
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A connection could not be finished: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        private bool IsLogged(INpConnection connection) => connection.Proxy.Traffic switch
        {
            NpTrafficKind.Http or NpTrafficKind.Https => true,
            NpTrafficKind.Tcp => _includeTcp,
            _ => false
        };

        private string Client(INpConnection connection, HttpHead head)
        {
            if (AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false)
            {
                return connection.ClientAddress;
            }

            return _trusted.Client(peer, head.Header("CF-Connecting-IP"), head.Header("X-Forwarded-For")).ToString();
        }

        private void Write(HttpExchange exchange)
        {
            if (_ignorePaths.Any(o => exchange.Path.StartsWith(o, StringComparison.OrdinalIgnoreCase)) || _ignoreClients.Contains(exchange.Client))
            {
                return;
            }

            _writer?.Write(AccessLogFormatter.Format(exchange, _format));
        }
    }
}
