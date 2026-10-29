using NetProxy.PluginApi;

using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.Maintenance
{
    /// <summary>
    /// Tells people that the site is down for maintenance, instead of letting their browser show a connection error:
    ///
    /// * When no endpoint of an HTTP or HTTPS proxy works (they are down, or refuse the connection) the client is answered with a page
    ///   and a 503. That needs nothing but the plug-in being enabled for the proxy.
    /// * With the setting Force on, every request is answered with the page, also when the endpoints work: for planned maintenance,
    ///   where the site is switched off by switching the setting on. Addresses in Allow still get through.
    /// * Planned windows (Window1, Weekly1, Daily1) switch the same on and off by themselves, with the end of the window as the
    ///   Retry-After. Force-style, a client that can not be given the page (HTTP/2) is not let through to the site either.
    ///
    /// The page is built from Title and Message, or is an HTML file of your own (PageFile).
    /// </summary>
    [NpPlugin("Maintenance Page", Version = "1.0", Author = "NetworkDLS",
        Description = "Answers with a 'down for maintenance' page when no endpoint of an HTTP proxy works, or on demand.")]
    public sealed class MaintenancePlugin : NpPlugin
    {
        private static readonly TimeSpan LogEvery = TimeSpan.FromMinutes(1);

        private readonly TimeProvider _clock;
        private readonly Dictionary<Guid, DateTime> _logged = [];
        private MaintenanceSettings _settings = MaintenanceSettings.From(new Dictionary<string, string>(), out _);
        private PageBuilder? _page;
        private string? _reportedProblem;
        private int _warnedHttp2;
        private bool _plannedWindowOn;

        public MaintenancePlugin() : this(TimeProvider.System)
        {
        }

        internal MaintenancePlugin(TimeProvider clock) => _clock = clock;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            _settings = MaintenanceSettings.From(Host.Settings, out var problems);

            foreach (var problem in problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _page = new PageBuilder(_settings, Host.DataDirectory, _clock);

            if (_settings.PageFile != null)
            {
                _page.Response(); //Reads the file now, so that a mistake is logged at the start and not at the first visitor.
                ReportPageProblem();
            }

            Host.Log(_settings.Force ? NpPluginLogLevel.Warning : NpPluginLogLevel.Information, _settings.Force
                ? $"Maintenance mode is ON: every request gets the {_settings.StatusCode} page{(_settings.Allow.Count > 0 ? $", except from {string.Join(", ", _settings.Allow)}" : string.Empty)}."
                : $"Answering with a {_settings.StatusCode} page when no endpoint works.");

            if (_settings.Schedule.IsEmpty == false)
            {
                var next = _settings.Schedule.NextStart(_clock.GetUtcNow().UtcDateTime);
                Host.Log(NpPluginLogLevel.Information, $"Planned windows: {_settings.Schedule}. {(next != null ? $"The next starts {next:u}." : "None starts in the next week.")}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnEndpointsUnavailableAsync(NpUnavailableEventArgs unavailable, CancellationToken cancellationToken)
        {
            try
            {
                var connection = unavailable.Connection;

                if (_page == null || unavailable.CanRespond == false || IsWeb(connection) == false)
                {
                    if (unavailable.CanRespond == false && connection.IsHttp2)
                    {
                        WarnAboutHttp2();
                    }

                    return ValueTask.CompletedTask;
                }

                unavailable.Respond(_page.Response());
                ReportPageProblem();

                var now = _clock.GetUtcNow().UtcDateTime;
                lock (_logged)
                {
                    if (_logged.TryGetValue(connection.Proxy.Id, out var last) == false || now - last >= LogEvery)
                    {
                        _logged[connection.Proxy.Id] = now;
                        Host.Log(NpPluginLogLevel.Warning,
                            $"No endpoint of proxy '{connection.Proxy.Name}' works, so {connection.ClientAddress} (and the clients after it, for a minute) gets the maintenance page.");
                    }
                }
            }
            catch (Exception ex)
            {
                //Nothing here is allowed to make things worse: without an answer the connection is closed, as it is without the plug-in.
                Host.Log(NpPluginLogLevel.Warning, $"The maintenance page could not be sent: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (_page == null || data.Direction != NpDataDirection.ClientToEndpoint || IsWeb(connection) == false || IsDown(out var plannedUntil) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (AddressRange.TryParseAddress(connection.ClientAddress, out var client) && _settings.IsAllowed(client))
                {
                    return ValueTask.CompletedTask;
                }

                if (data.CanRespond == false)
                {
                    //The site is meant to be down, so a client that can not be given the page is not let through to it either.
                    if (connection.IsHttp2)
                    {
                        WarnAboutHttp2();
                    }

                    data.CloseConnection("The site is down for maintenance.");
                    return ValueTask.CompletedTask;
                }

                //The client is answered and the connection is closed, so that nothing of the request (a body that is still to come) reaches the endpoint.
                data.Respond(_page.Response(RetryAfter(plannedUntil)), closeConnection: true);
                ReportPageProblem();
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Warning, $"The maintenance page could not be sent: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// True while the site is meant to be down: Force is on, or a planned window is on. <paramref name="plannedUntil"/> is when the
        /// window ends (null for Force, which has no end).
        /// </summary>
        private bool IsDown(out DateTime? plannedUntil)
        {
            plannedUntil = _settings.Force ? null : _settings.Schedule.ActiveUntil(_clock.GetUtcNow().UtcDateTime);
            return _settings.Force || plannedUntil != null;
        }

        /// <summary>When to tell clients to come back: when the window ends, if that is known.</summary>
        private int? RetryAfter(DateTime? plannedUntil)
            => plannedUntil == null ? null : (int)Math.Clamp(Math.Ceiling((plannedUntil.Value - _clock.GetUtcNow().UtcDateTime).TotalSeconds), 1, 86_400);

        public override TimeSpan? TickInterval => TimeSpan.FromSeconds(30);

        /// <summary>
        /// Says in the log when a planned window starts and when it ends.
        /// </summary>
        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            var until = _settings.Force ? null : _settings.Schedule.ActiveUntil(_clock.GetUtcNow().UtcDateTime);
            var active = until != null;

            if (active != _plannedWindowOn)
            {
                _plannedWindowOn = active;
                Host.Log(active ? NpPluginLogLevel.Warning : NpPluginLogLevel.Information, active
                    ? $"The planned maintenance window has started: every request gets the {_settings.StatusCode} page until {until:u}."
                    : "The planned maintenance window has ended: the site is back.");
            }

            return ValueTask.CompletedTask;
        }

        private static bool IsWeb(INpConnection connection) => connection.Proxy.Traffic is NpTrafficKind.Http or NpTrafficKind.Https;

        private void WarnAboutHttp2()
        {
            if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
            {
                Host.Log(NpPluginLogLevel.Warning,
                    "A client uses HTTP/2 and can not be given the page: the connection is closed. Untick Allow HTTP/2 on the proxy to have those clients given it.");
            }
        }

        /// <summary>
        /// Says once when the page file can not be used.
        /// </summary>
        private void ReportPageProblem()
        {
            var problem = _page?.Problem;

            if (problem != null && problem != _reportedProblem)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _reportedProblem = problem;
        }
    }
}
