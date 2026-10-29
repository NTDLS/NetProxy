using NetProxy.PluginApi;
using NetProxy.Plugins.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace NetProxy.Plugins.Redirect
{
    /// <summary>
    /// Sends clients somewhere else, by rules: http to https, an old address to a new one, one site to another. The rules are the settings
    /// Rule1, Rule2, ... and the first one that fits a request answers it. The endpoint never sees a request that is redirected.
    ///
    ///   Rule1=http://ntdls.com/page.html -> https://ntdls.com/page.html
    ///   Rule2=http://example.com/page.html -> http://example.com
    ///   Rule3=http://*/* -> https://{host}{url}
    ///
    /// It also answers the HTTP-01 challenge of an ACME client (Let's Encrypt) from a folder, so that a certificate can be renewed on a
    /// port that is otherwise redirected: set AcmeDirectory to the folder that the ACME client writes its challenge files to.
    /// </summary>
    [NpPlugin("Redirect", Version = "1.0", Author = "NetworkDLS",
        Description = "Redirects requests by rules (http to https, old address to new), and answers ACME HTTP-01 challenges.")]
    public sealed class RedirectPlugin : NpPlugin
    {
        internal const string StateKey = "NetProxy.Plugins.Redirect";
        private const string AcmePrefix = "/.well-known/acme-challenge/";

        private static readonly Regex AcmeToken = new("^[A-Za-z0-9_-]{1,256}$", RegexOptions.Compiled);

        private readonly List<RedirectRule> _rules = [];
        private int _defaultStatus = 301;
        private string? _acmeDirectory;
        private int _warnedHttp2;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            var settings = new SettingsReader(Host.Settings);

            _defaultStatus = settings.Number("Status", 301, 301, 308);
            if (Array.IndexOf([301, 302, 303, 307, 308], _defaultStatus) < 0)
            {
                settings.Problems.Add($"The setting Status must be 301, 302, 303, 307 or 308; {_defaultStatus} is not, so 301 is used.");
                _defaultStatus = 301;
            }

            _acmeDirectory = settings.Text("AcmeDirectory");
            if (_acmeDirectory != null)
            {
                _acmeDirectory = Path.GetFullPath(_acmeDirectory, Host.DataDirectory);
                if (Directory.Exists(_acmeDirectory) == false)
                {
                    settings.Problems.Add($"The folder of the setting AcmeDirectory ('{_acmeDirectory}') does not exist yet. Challenges are answered once it does.");
                }
            }

            _rules.Clear();
            foreach (var (name, text) in settings.Numbered("Rule"))
            {
                var rule = RedirectRule.Parse(text, out var error);
                if (rule == null)
                {
                    settings.Problems.Add($"{name} is left out. {error}");
                }
                else
                {
                    _rules.Add(rule);
                }
            }

            foreach (var problem in settings.Problems)
            {
                Host.Log(NpPluginLogLevel.Warning, problem);
            }

            Host.Log(NpPluginLogLevel.Information, $"{_rules.Count} redirect rule(s){(_acmeDirectory != null ? $", ACME challenges from {_acmeDirectory}" : string.Empty)}.");
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            try
            {
                var connection = data.Connection;

                if (data.Direction != NpDataDirection.ClientToEndpoint || connection.Proxy.Traffic == NpTrafficKind.Udp || (_rules.Count == 0 && _acmeDirectory == null))
                {
                    return ValueTask.CompletedTask;
                }

                if (connection.IsHttp2)
                {
                    if (Interlocked.Exchange(ref _warnedHttp2, 1) == 0)
                    {
                        Host.Log(NpPluginLogLevel.Warning,
                            "A client uses HTTP/2, which is compressed, so its requests can not be redirected. Untick Allow HTTP/2 on the proxy to have them redirected.");
                    }

                    return ValueTask.CompletedTask;
                }

                if (connection.State.TryGetValue(StateKey, out var existing) == false)
                {
                    existing = new RequestReader(keepBuffer: true);
                    connection.State[StateKey] = existing;
                }

                var requests = HeadGate.Feed((RequestReader)existing!, data);

                foreach (var request in requests)
                {
                    var response = Answer(connection, request, out var describe);
                    if (response == null)
                    {
                        continue;
                    }

                    if (data.CanRespond == false)
                    {
                        return ValueTask.CompletedTask; //Nothing to answer with: it goes to the endpoint as it is.
                    }

                    //A request that is answered while others follow it in the same read can not be answered in order, so the client asks again.
                    var close = requests.Count > 1 || request.BodyPending
                        || (request.Header("Connection")?.Contains("close", StringComparison.OrdinalIgnoreCase) ?? false);

                    data.Respond(response(close), close);
                    Host.Log(NpPluginLogLevel.Verbose, $"{connection.ClientAddress}: {describe}");
                    break;
                }
            }
            catch (Exception ex)
            {
                //A rule that fails must not get in the way of the traffic: the request goes to the endpoint.
                Host.Log(NpPluginLogLevel.Warning, $"A request could not be checked for redirects: {ex.GetBaseException().Message}");
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// The response for a request (made once it is known whether the connection is to be closed), or null when it is not for this plug-in.
        /// </summary>
        private Func<bool, byte[]>? Answer(INpConnection connection, SeenRequest request, out string describe)
        {
            describe = string.Empty;

            if (_acmeDirectory != null && Challenge(request) is { } challenge)
            {
                describe = $"answered the ACME challenge {request.Path}";
                return close => HttpResponse.Build(200, "text/plain; charset=utf-8", challenge, close, [("Cache-Control", "no-store")]);
            }

            if (_rules.Count == 0)
            {
                return null;
            }

            var scheme = connection.ClientTls != null ? "https" : "http";
            var host = request.Host ?? connection.HostName ?? string.Empty;
            var port = scheme == "https" ? 443 : 80;

            if (host.StartsWith('[') == false && host.LastIndexOf(':') is > 0 and var colon && int.TryParse(host[(colon + 1)..], out var hostPort))
            {
                port = hostPort;
                host = host[..colon];
            }
            else if (host.StartsWith('[') && host.IndexOf("]:", StringComparison.Ordinal) is > 0 and var bracket && int.TryParse(host[(bracket + 2)..], out var ipv6Port))
            {
                port = ipv6Port;
                host = host[..(bracket + 1)];
            }

            var url = new RequestedUrl(scheme, host.TrimEnd('.').ToLowerInvariant(), port, request.Path, request.Query);

            foreach (var rule in _rules)
            {
                if (rule.Apply(url) is { } location)
                {
                    var status = rule.Status != 0 ? rule.Status : _defaultStatus;
                    describe = $"{url} -> {location} ({status})";
                    return close => HttpResponse.Build(status, "text/plain; charset=utf-8", [], close, [("Location", location), ("Cache-Control", "no-store")]);
                }
            }

            return null;
        }

        /// <summary>
        /// The contents of the challenge file for a request for /.well-known/acme-challenge/token, or null.
        /// </summary>
        private byte[]? Challenge(SeenRequest request)
        {
            if (request.Method != "GET" || request.Path.StartsWith(AcmePrefix, StringComparison.Ordinal) == false)
            {
                return null;
            }

            var token = request.Path[AcmePrefix.Length..];
            if (AcmeToken.IsMatch(token) == false) //Also keeps the request inside the folder.
            {
                return null;
            }

            var file = Path.Combine(_acmeDirectory!, token);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }
    }
}
