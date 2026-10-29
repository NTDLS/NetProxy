using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Net;
using System.Text;

namespace NetProxy.Plugins.AntiHammer
{
    /// <summary>
    /// Bans the addresses that hammer a proxy for a while: too many connections or requests, the same page over and over, or requests for
    /// pages that only something that looks for a weakness asks for (/.env, /wp-login.php, ...). Repeat offenders are banned for longer
    /// every time.
    ///
    /// A banned address is turned away when it connects. When the NetProxy is behind a proxy that is trusted (Cloudflare, say: see the
    /// TrustedProxies setting) every client connects from that proxy, so the client is found in CF-Connecting-IP or X-Forwarded-For
    /// and is turned away with a 403 instead.
    ///
    /// It starts in log-only mode: it logs the bans that it would make, and makes none, until LogOnly is set to false.
    /// </summary>
    [NpPlugin("Anti-Hammer", Version = "1.0", Author = "NetworkDLS",
        Description = "Bans the addresses that hammer a proxy for a while: too many requests, the same page again and again, or probes for pages that are not there.")]
    public sealed class AntiHammerPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.AntiHammer.Reader";

        private readonly TimeProvider _clock;
        private AntiHammerSettings _settings = AntiHammerSettings.From(new Dictionary<string, string>(), out _);
        private HammerGuard _guard;
        private string? _banFile;
        private long _blocked;
        private int _warnedHttp2;

        public AntiHammerPlugin() : this(TimeProvider.System)
        {
        }

        internal AntiHammerPlugin(TimeProvider clock)
        {
            _clock = clock;
            _guard = new HammerGuard(_settings, clock);
        }

        public override TimeSpan? TickInterval => TimeSpan.FromSeconds(30);

        internal HammerGuard Guard => _guard;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            _settings = AntiHammerSettings.From(Host.Settings, out var problems);

            foreach (var problem in problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            _guard = new HammerGuard(_settings, _clock);
            _banFile = Path.Combine(Host.DataDirectory, "bans.json");

            try
            {
                var active = _guard.Bans.Load(_banFile);
                if (active > 0)
                {
                    Host.Log(NpPluginLogLevel.Information, $"{active} ban(s) from before are still in force.");
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Warning, $"The bans that were saved could not be read, so there are none: {ex.GetBaseException().Message}");
            }

            Host.Log(NpPluginLogLevel.Information, _settings.LogOnly
                ? "Log-only: addresses that would be banned are logged, and not banned. Set LogOnly to false to ban them."
                : $"Banning for {_settings.BanMinutes} minute(s){(_settings.Escalate ? ", twice as long every time an address is back" : string.Empty)}.");

            if (_settings.HasTrustedProxies == false)
            {
                Host.Log(NpPluginLogLevel.Information, "No TrustedProxies are set, so the address that connects is the client. Behind Cloudflare set it to cloudflare.");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken)
        {
            Save();
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnTickAsync(CancellationToken cancellationToken)
        {
            try
            {
                _guard.Maintain();
                Save();

                var blocked = Interlocked.Exchange(ref _blocked, 0);
                if (blocked > 0)
                {
                    Host.Log(NpPluginLogLevel.Information, $"Turned away {blocked} connection(s) and request(s) from {_guard.Bans.ActiveCount} banned address(es) since the last report.");
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Warning, $"Could not do the upkeep of the bans: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                if (connection.Proxy.Traffic == NpTrafficKind.Udp
                    || AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false
                    || _settings.IsAllowed(peer)
                    //Everyone connects from a proxy that is trusted. The clients are told apart by their requests.
                    || _settings.IsTrustedProxy(peer))
                {
                    return ValueTask.FromResult(NpVerdict.Allow);
                }

                var ban = _guard.FindBan(peer);
                if (ban != null)
                {
                    return ValueTask.FromResult(Turn(ban));
                }

                ban = _guard.CountConnection(peer);
                if (ban != null)
                {
                    Announce(ban, peer, connection, null);
                    return ValueTask.FromResult(Turn(ban));
                }
            }
            catch (Exception ex)
            {
                //Guarding is never allowed to get in the way of the traffic.
                Host.Log(NpPluginLogLevel.Verbose, $"A connection could not be checked: {ex.GetBaseException().Message}");
            }

            return ValueTask.FromResult(NpVerdict.Allow);
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (data.Direction != NpDataDirection.ClientToEndpoint
                    || connection.Proxy.Traffic is not (NpTrafficKind.Http or NpTrafficKind.Https)
                    || AddressRange.TryParseAddress(connection.ClientAddress, out var peer) == false)
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning,
                            "A client uses HTTP/2, which is compressed, so its requests are not counted (its connections are). Untick Allow HTTP/2 on the proxy to have them counted.");
                    }

                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    existing = new RequestReader();
                    connection.State[StateKey] = existing;
                }

                var viaProxy = _settings.IsTrustedProxy(peer);

                foreach (var request in ((RequestReader)existing!).Feed(data.Data.Span))
                {
                    var client = viaProxy ? ClientBehindProxy(peer, request) : peer;
                    if (_settings.IsAllowed(client))
                    {
                        continue;
                    }

                    var ban = _guard.FindBan(client);
                    if (ban == null)
                    {
                        ban = _guard.CountRequest(client, request.Target);
                        if (ban != null)
                        {
                            Announce(ban, client, connection, request);
                        }
                    }

                    if (ban != null && Turn(ban).Allowed == false)
                    {
                        //The connection of a proxy is shared with its other clients, so it stays open if the request is whole.
                        var close = viaProxy == false || request.BodyPending;

                        if (data.CanRespond)
                        {
                            data.Respond(Forbidden(ban, close), close);
                        }
                        else
                        {
                            data.CloseConnection($"{client} is banned.");
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Host.Log(NpPluginLogLevel.Verbose, $"A request could not be checked: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// The client of a request that came through a proxy that is trusted: CF-Connecting-IP if it has one, otherwise the last address
        /// of X-Forwarded-For that is not a trusted proxy itself (the ones before it were sent by the client, which can send anything).
        /// </summary>
        internal IPAddress ClientBehindProxy(IPAddress peer, SeenRequest request)
        {
            if (AddressRange.TryParseAddress(request.ConnectingIp, out var connecting))
            {
                return connecting;
            }

            if (request.ForwardedFor != null)
            {
                foreach (var entry in request.ForwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
                {
                    if (AddressRange.TryParseAddress(entry, out var address) == false)
                    {
                        break;
                    }

                    if (_settings.IsTrustedProxy(address) == false)
                    {
                        return address;
                    }
                }
            }

            return peer;
        }

        /// <summary>
        /// What to do about an address that is banned: nothing when only logging, and turning it away when banning.
        /// </summary>
        private NpVerdict Turn(BanEntry ban)
        {
            if (_settings.LogOnly)
            {
                return NpVerdict.Allow;
            }

            Interlocked.Increment(ref ban.Blocked);
            Interlocked.Increment(ref _blocked);
            return NpVerdict.Deny($"{ban.Address} is banned until {ban.ExpiresUtc:u}: {ban.Reason}.");
        }

        private void Announce(BanEntry ban, IPAddress client, INpConnection connection, SeenRequest? request)
        {
            var where = request == null
                ? $"proxy '{connection.Proxy.Name}'"
                : $"proxy '{connection.Proxy.Name}', {request.Host ?? connection.HostName ?? "no host"}{HammerGuard.PathOf(request.Target)}";

            Host.Log(NpPluginLogLevel.Warning, _settings.LogOnly
                ? $"Would ban {client} for {Describe(ban.Duration)}: {ban.Reason} ({where})."
                : $"Banned {client} for {Describe(ban.Duration)} (offence {ban.Offences}): {ban.Reason} ({where}).");
        }

        private static string Describe(TimeSpan span)
            => span.TotalHours >= 1 ? $"{span.TotalHours:0.#} hour(s)" : $"{Math.Max(span.TotalMinutes, 1):0.#} minute(s)";

        private byte[] Forbidden(BanEntry ban, bool close)
        {
            var body = "Too many requests, or requests for pages that are not here. Try again later.";
            var retryAfter = Math.Max((int)Math.Ceiling((ban.ExpiresUtc - _clock.GetUtcNow().UtcDateTime).TotalSeconds), 1);

            return Encoding.ASCII.GetBytes(
                "HTTP/1.1 403 Forbidden\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                $"Retry-After: {retryAfter}\r\n" +
                $"Connection: {(close ? "close" : "keep-alive")}\r\n\r\n" + body);
        }

        private void Save()
        {
            //Bans that are only logged are not kept: they would be bans when the plug-in is switched on.
            if (_banFile != null && _settings.LogOnly == false && _guard.Bans.IsDirty)
            {
                _guard.Bans.Save(_banFile);
            }
        }
    }
}
