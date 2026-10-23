using NetProxy.PluginApi;
using System.Text;

namespace NetProxy.SamplePlugin
{
    /// <summary>
    /// A small plug-in that shows how the plug-in API is used. Settings (name and value pairs in the plugin configuration):
    ///
    ///   BlockClients   Comma separated client addresses that are not allowed to connect.
    ///   Replace        Text to find and replace in the data, as find=>replace. Works on the decrypted data of HTTPS proxies.
    ///   LogData        True to log the first bytes of every read (as text).
    ///   MaxLoggedBytes How many bytes of each read to log (default 200).
    /// </summary>
    [NpPlugin("Traffic Inspector", Version = "1.0", Author = "NetworkDLS",
        Description = "Logs connections, can block clients, and can look at and change the data that passes through (after TLS is decrypted).")]
    public class TrafficInspectorPlugin : NpPlugin
    {
        private HashSet<string> _blocked = [];
        private string? _find;
        private string _replace = string.Empty;
        private bool _logData;
        private int _maxLoggedBytes = 200;

        public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
        {
            _blocked = Host.Settings.TryGetValue("BlockClients", out var blocked)
                ? blocked.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet()
                : [];

            if (Host.Settings.TryGetValue("Replace", out var replace) && replace.Contains("=>"))
            {
                var parts = replace.Split("=>", 2);
                _find = parts[0];
                _replace = parts[1];
            }

            _logData = Host.Settings.TryGetValue("LogData", out var logData) && bool.TryParse(logData, out var on) && on;

            if (Host.Settings.TryGetValue("MaxLoggedBytes", out var max) && int.TryParse(max, out var parsed) && parsed > 0)
            {
                _maxLoggedBytes = parsed;
            }

            Host.Log(NpPluginLogLevel.Information, $"Ready. {_blocked.Count} blocked clients, replace {(_find == null ? "off" : $"'{_find}'")}, log data {_logData}.");
            return ValueTask.CompletedTask;
        }

        public override ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
        {
            if (_blocked.Contains(connection.ClientAddress))
            {
                return ValueTask.FromResult(NpVerdict.Deny($"{connection.ClientAddress} is blocked"));
            }

            Host.Log(NpPluginLogLevel.Information,
                $"{connection.ClientAddress}:{connection.ClientPort} connecting to proxy '{connection.Proxy.Name}' for host '{connection.HostName ?? "(unknown)"}'.");
            return ValueTask.FromResult(NpVerdict.Allow);
        }

        public override ValueTask OnConnectedAsync(INpConnection connection, CancellationToken cancellationToken)
        {
            Host.Log(NpPluginLogLevel.Information,
                $"{connection.ClientAddress} is connected to {connection.Endpoint?.Address}:{connection.Endpoint?.Port} (client TLS: {connection.ClientTls ?? "none"}).");
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
        {
            Host.Log(NpPluginLogLevel.Information,
                $"{connection.ClientAddress} disconnected ({info.Reason}) after {info.Duration.TotalSeconds:n1}s: {info.BytesFromClient} bytes in, {info.BytesFromEndpoint} out.");
            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            if (_logData)
            {
                var length = Math.Min(data.Data.Length, _maxLoggedBytes);
                var text = Encoding.UTF8.GetString(data.Data.Span[..length]).Replace("\r", "\\r").Replace("\n", "\\n");
                Host.Log(NpPluginLogLevel.Verbose,
                    $"{data.Direction} {data.Data.Length} bytes{(data.ReceivedEncrypted ? " (decrypted)" : "")}: {text}");
            }

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnDataSendingAsync(NpDataEventArgs data, CancellationToken cancellationToken)
        {
            if (_find != null)
            {
                //The data is a read, so a word can be split across two. That is fine for a sample.
                var text = Encoding.UTF8.GetString(data.Data.Span);
                if (text.Contains(_find, StringComparison.Ordinal))
                {
                    data.Replace(Encoding.UTF8.GetBytes(text.Replace(_find, _replace, StringComparison.Ordinal)));
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}
