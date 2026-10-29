using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Collections.Concurrent;
using System.Net.Mail;

namespace NetProxy.Plugins.Alerts
{
    /// <summary>
    /// Tells you when something is wrong, in Discord, Slack, a webhook of your own, or an e-mail:
    ///
    /// * an endpoint is down (it could not be connected to FailuresBeforeDown times in a row), and when it is back;
    /// * no endpoint of a proxy works at all;
    /// * a proxy could not start (the port is in use, say), and when it does;
    /// * a plug-in or a connection failed;
    /// * someone failed to log in to the management interface several times.
    ///
    /// The same thing is told once in MinSecondsBetween (5 minutes), with a count of how many were held back. Alerts are sent from a task
    /// of their own, so a slow webhook never slows the traffic. A plug-in that answers for a proxy that has no endpoint (the Maintenance
    /// plug-in) ends the "no endpoint works" event for the plug-ins after it, so put this plug-in first for a proxy that uses both.
    /// An endpoint is only found to be down when a client tries it: one that nobody asks for is not noticed.
    /// </summary>
    [NpPlugin("Alerts", Version = "1.0", Author = "NetworkDLS",
        Description = "Tells you in Discord, Slack, a webhook or an e-mail when an endpoint is down, a proxy fails to start, or someone fails to log in.")]
    public sealed class AlertsPlugin : NpPlugin
    {
        private sealed class EndpointState
        {
            public int Failures;
            public DateTime? DownSince;
        }

        private readonly HttpMessageHandler? _handler;
        private readonly TimeProvider _clock;
        private readonly Func<MailMessage, CancellationToken, Task>? _mail;
        private readonly ConcurrentDictionary<Guid, EndpointState> _endpoints = new();
        private readonly ConcurrentDictionary<Guid, bool> _failedToStart = new();
        private readonly List<DateTime> _failedLogins = [];

        private HttpClient? _http;
        private AlertDispatcher? _dispatcher;
        private string _source = Environment.MachineName;
        private int _failuresBeforeDown = 3;
        private int _failedLoginsBeforeAlert = 3;
        private bool _alertOnErrors = true;

        public AlertsPlugin() : this(null, TimeProvider.System, null)
        {
        }

        internal AlertsPlugin(HttpMessageHandler? handler, TimeProvider clock, Func<MailMessage, CancellationToken, Task>? mail)
        {
            _handler = handler;
            _clock = clock;
            _mail = mail;
        }

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _source = settings.Text("Source", Environment.MachineName);
            _failuresBeforeDown = settings.Number("FailuresBeforeDown", 3, 1, 1000);
            _failedLoginsBeforeAlert = settings.Number("FailedLoginsBeforeAlert", 3, 1, 1000);
            _alertOnErrors = settings.Flag("AlertOnErrors", true);
            var minGap = TimeSpan.FromSeconds(settings.Number("MinSecondsBetween", 300, 0, 86_400));

            _http = _handler != null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(15);

            var channels = new List<IAlertChannel>();

            if (Url(settings, "Discord.Url") is { } discord)
            {
                channels.Add(new DiscordChannel(_http, discord));
            }

            if (Url(settings, "Slack.Url") is { } slack)
            {
                channels.Add(new SlackChannel(_http, slack));
            }

            if (Url(settings, "Webhook.Url") is { } webhook)
            {
                channels.Add(new WebhookChannel(_http, webhook, settings.Text("Webhook.Authorization")));
            }

            if (settings.Has("Smtp.Host") || settings.Has("Smtp.To"))
            {
                var to = settings.List("Smtp.To");
                var from = settings.Text("Smtp.From");

                if (settings.Has("Smtp.Host") == false || to.Count == 0 || from == null)
                {
                    settings.Problems.Add("E-mail needs Smtp.Host, Smtp.From and Smtp.To, so it is not used.");
                }
                else
                {
                    try
                    {
                        _ = new MailAddress(from);
                        to.ForEach(o => _ = new MailAddress(o));

                        var tls = settings.Flag("Smtp.Tls", true);
                        channels.Add(new SmtpChannel(new SmtpSettings(settings.Text("Smtp.Host")!, settings.Number("Smtp.Port", tls ? 587 : 25, 1, 65535),
                            settings.Text("Smtp.User"), settings.Text("Smtp.Password"), from, to, tls), _mail));
                    }
                    catch (FormatException ex)
                    {
                        settings.Problems.Add($"An e-mail address in Smtp.From or Smtp.To is not one, so e-mail is not used: {ex.Message}");
                    }
                }
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _dispatcher = new AlertDispatcher(channels, $"NetProxy on {_source}", minGap, _clock, (warn, text) => Host.Log(warn ? NpPluginLogLevel.Warning : NpPluginLogLevel.Information, text));
            _dispatcher.Start();

            if (channels.Count == 0)
            {
                Host.Log(NpPluginLogLevel.Warning, "No place to send alerts to is set: set Discord.Url, Slack.Url, Webhook.Url or Smtp.Host, Smtp.From and Smtp.To.");
            }
            else
            {
                Host.Log(NpPluginLogLevel.Information, $"Alerts go to {string.Join(", ", channels.Select(o => o.Name))}.");

                if (settings.Flag("NotifyOnStart", false))
                {
                    Tell("start", AlertSeverity.Information, "NetProxy started", $"The Alerts plug-in is running on {_source}.");
                }
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            _dispatcher?.Stop(TimeSpan.FromSeconds(3));
            _dispatcher = null;
            _http?.Dispose();
            _http = null;
            return ValueTask.CompletedTask;
        }

        private string? Url(SettingsReader settings, string name)
        {
            if (settings.Text(name) is not { } url)
            {
                return null;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                return url;
            }

            settings.Problems.Add($"The setting {name} must be an http:// or https:// address, so it is not used.");
            return null;
        }

        private void Tell(string key, AlertSeverity severity, string title, string text)
            => _dispatcher?.Tell(new AlertMessage(key, severity, title, text, _clock.GetUtcNow().UtcDateTime));

        //---------------------------------------------------------------- Endpoints

        public override ValueTask OnEndpointConnectFailedAsync(INpConnection connection, NpEndpointInfo endpoint, string error, CancellationToken cancellationToken)
        {
            var state = _endpoints.GetOrAdd(endpoint.Id, _ => new EndpointState());

            bool down;
            lock (state)
            {
                state.Failures++;
                down = state.Failures >= _failuresBeforeDown && state.DownSince == null;

                if (down)
                {
                    state.DownSince = _clock.GetUtcNow().UtcDateTime;
                }
            }

            if (down)
            {
                Tell($"endpoint:{endpoint.Id}", AlertSeverity.Critical, $"Endpoint down: {endpoint.Address}:{endpoint.Port}",
                    $"Proxy '{connection.Proxy.Name}' could not connect to {endpoint.Address}:{endpoint.Port} {_failuresBeforeDown} time(s) in a row.\nLast error: {error}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnEndpointConnectedAsync(INpConnection connection, NpEndpointInfo endpoint, CancellationToken cancellationToken)
        {
            if (_endpoints.TryGetValue(endpoint.Id, out var state) == false)
            {
                return ValueTask.CompletedTask;
            }

            DateTime? downSince;
            lock (state)
            {
                downSince = state.DownSince;
                state.DownSince = null;
                state.Failures = 0;
            }

            if (downSince != null)
            {
                var downtime = _clock.GetUtcNow().UtcDateTime - downSince.Value;
                Tell($"endpoint:{endpoint.Id}", AlertSeverity.Recovered, $"Endpoint back: {endpoint.Address}:{endpoint.Port}",
                    $"Proxy '{connection.Proxy.Name}' is connected to {endpoint.Address}:{endpoint.Port} again after {Describe(downtime)}.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnEndpointsUnavailableAsync(NpUnavailableEventArgs unavailable, CancellationToken cancellationToken)
        {
            var proxy = unavailable.Connection.Proxy;
            var tried = unavailable.Failures.Count == 0
                ? "It has no endpoints that are switched on."
                : "Tried: " + string.Join("; ", unavailable.Failures.Select(o => $"{o.Endpoint.Address}:{o.Endpoint.Port} ({o.Error})"));

            Tell($"unavailable:{proxy.Id}", AlertSeverity.Critical, $"No endpoint works: {proxy.Name}", $"Proxy '{proxy.Name}' (port {proxy.ListenPort}) can not connect {unavailable.Connection.ClientAddress} to anything.\n{tried}");
            return ValueTask.CompletedTask;
        }

        //---------------------------------------------------------------- Proxies and the service

        public override ValueTask OnProxyStartFailedAsync(NpProxyInfo proxy, string error, CancellationToken cancellationToken)
        {
            _failedToStart[proxy.Id] = true;
            Tell($"start:{proxy.Id}", AlertSeverity.Critical, $"Proxy could not start: {proxy.Name}", $"Proxy '{proxy.Name}' (port {proxy.ListenPort}) did not start.\n{error}");
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnProxyStartedAsync(NpProxyInfo proxy, IReadOnlyList<string> listeningOn, CancellationToken cancellationToken)
        {
            if (_failedToStart.TryRemove(proxy.Id, out _))
            {
                Tell($"start:{proxy.Id}", AlertSeverity.Recovered, $"Proxy started: {proxy.Name}", $"Proxy '{proxy.Name}' is listening on {string.Join(", ", listeningOn)}.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnErrorAsync(NpPluginError error, CancellationToken cancellationToken)
        {
            if (_alertOnErrors)
            {
                var key = error.Message.Length <= 80 ? error.Message : error.Message[..80];
                Tell($"error:{key}", AlertSeverity.Warning, "NetProxy reported an error",
                    $"{error.Message}{(error.Proxy != null ? $"\nProxy: {error.Proxy.Name}" : string.Empty)}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnManagementLoginAsync(NpLoginInfo login, CancellationToken cancellationToken)
        {
            if (login.Succeeded)
            {
                return ValueTask.CompletedTask;
            }

            int count;
            lock (_failedLogins)
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                _failedLogins.RemoveAll(o => now - o > TimeSpan.FromMinutes(10));
                _failedLogins.Add(now);
                count = _failedLogins.Count;

                if (count >= _failedLoginsBeforeAlert)
                {
                    _failedLogins.Clear();
                }
            }

            if (count >= _failedLoginsBeforeAlert)
            {
                Tell("logins", AlertSeverity.Warning, "Failed logins to the management interface", $"{count} failed logins in the last 10 minutes. The last was for the user '{login.UserName}'.");
            }

            return ValueTask.CompletedTask;
        }

        private static string Describe(TimeSpan span)
            => span.TotalHours >= 1 ? $"{span.TotalHours:0.#} hour(s)" : span.TotalMinutes >= 1 ? $"{span.TotalMinutes:0.#} minute(s)" : $"{Math.Max(span.TotalSeconds, 1):0} second(s)";
    }
}
