using NetProxy.PluginApi;
using System.Text;

// These are the plugins that the tests load. They are loaded the way any plugin is (from the file of this assembly, in a load context of
// their own), so the tests see what they do through the file they write and through what happens to the traffic.

/// <summary>
/// Writes down every event it gets, and misbehaves the way its settings say:
///   LogFile (where it writes), Throw, SleepMs, CloseOnData (text), DropOnData (text), Replace (find=>replace), DenyClients (comma list),
///   DenyProxyStart, DenyEndpointPort, Tag (written in front of each line).
/// </summary>
[NpPlugin("Recorder", Version = "1.2", Author = "tests", Description = "Writes down what happens.")]
public class RecordingPlugin : NpPlugin
{
    private static readonly object _lock = new();
    private string? _file;
    private string _tag = "";
    private bool _throw;
    private int _sleep;
    private string? _closeOn, _dropOn, _find, _replace;
    private HashSet<string> _denyClients = [];
    private bool _denyProxyStart;
    private int _denyEndpointPort;
    private int _tickSeconds;
    private int _failOnStart;

    public override TimeSpan? TickInterval => _tickSeconds > 0 ? TimeSpan.FromSeconds(_tickSeconds) : null;

    private void Write(string text)
    {
        if (_file != null)
        {
            lock (_lock)
            {
                File.AppendAllText(_file, $"{_tag}{text}\n");
            }
        }
    }

    private async ValueTask Misbehave(CancellationToken cancellationToken)
    {
        if (_throw)
        {
            throw new InvalidOperationException("boom");
        }

        if (_sleep > 0)
        {
            await Task.Delay(_sleep, cancellationToken);
        }
    }

    public override ValueTask OnServiceStartingAsync(CancellationToken cancellationToken)
    {
        var settings = Host.Settings;
        _file = settings.GetValueOrDefault("LogFile");
        _tag = settings.GetValueOrDefault("Tag") is { Length: > 0 } tag ? tag + ":" : "";
        _throw = settings.GetValueOrDefault("Throw") == "true";
        _sleep = int.TryParse(settings.GetValueOrDefault("SleepMs"), out var sleep) ? sleep : 0;
        _closeOn = settings.GetValueOrDefault("CloseOnData");
        _dropOn = settings.GetValueOrDefault("DropOnData");
        _denyClients = (settings.GetValueOrDefault("DenyClients") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        _denyProxyStart = settings.GetValueOrDefault("DenyProxyStart") == "true";
        _denyEndpointPort = int.TryParse(settings.GetValueOrDefault("DenyEndpointPort"), out var deny) ? deny : 0;
        _tickSeconds = int.TryParse(settings.GetValueOrDefault("TickSeconds"), out var tick) ? tick : 0;
        _failOnStart = int.TryParse(settings.GetValueOrDefault("FailOnStart"), out var fail) ? fail : 0;

        if (settings.GetValueOrDefault("Replace") is { } replace && replace.Contains("=>"))
        {
            var parts = replace.Split("=>", 2);
            _find = parts[0];
            _replace = parts[1];
        }

        if (_failOnStart > 0)
        {
            throw new InvalidOperationException("cannot start");
        }

        Write($"ServiceStarting name={Host.InstanceName} dir={Directory.Exists(Host.DataDirectory)}");
        return ValueTask.CompletedTask;
    }

    public override ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken) { Write("ServiceStopping"); return default; }
    public override ValueTask OnTickAsync(CancellationToken cancellationToken) { Write("Tick"); return default; }
    public override ValueTask OnManagementLoginAsync(NpLoginInfo login, CancellationToken cancellationToken) { Write($"Login {login.UserName} {login.Succeeded}"); return default; }
    public override ValueTask OnErrorAsync(NpPluginError error, CancellationToken cancellationToken) { Write($"Error {error.Message}"); return default; }

    public override ValueTask<NpVerdict> OnProxyStartingAsync(NpProxyInfo proxy, CancellationToken cancellationToken)
    {
        Write($"ProxyStarting {proxy.Name} {proxy.Traffic}");
        return ValueTask.FromResult(_denyProxyStart ? NpVerdict.Deny("not today") : NpVerdict.Allow);
    }

    public override ValueTask OnProxyStartedAsync(NpProxyInfo proxy, IReadOnlyList<string> listeningOn, CancellationToken cancellationToken)
    { Write($"ProxyStarted {proxy.Name} on={listeningOn.Count}"); return default; }

    public override ValueTask OnProxyStartFailedAsync(NpProxyInfo proxy, string error, CancellationToken cancellationToken)
    { Write($"ProxyStartFailed {proxy.Name} {error}"); return default; }

    public override ValueTask OnProxyStoppingAsync(NpProxyInfo proxy, CancellationToken cancellationToken) { Write($"ProxyStopping {proxy.Name}"); return default; }
    public override ValueTask OnProxyStoppedAsync(NpProxyInfo proxy, CancellationToken cancellationToken) { Write($"ProxyStopped {proxy.Name}"); return default; }

    public override ValueTask OnProxyConfigurationChangedAsync(NpProxyInfo proxy, NpChangeKind change, CancellationToken cancellationToken)
    { Write($"ProxyChanged {proxy.Name} {change}"); return default; }

    public override ValueTask OnConnectionRefusedAsync(NpProxyInfo proxy, string clientAddress, string reason, CancellationToken cancellationToken)
    { Write($"Refused {clientAddress}"); return default; }

    public override ValueTask OnClientTlsEstablishedAsync(INpConnection connection, CancellationToken cancellationToken)
    { Write($"ClientTls tls={connection.ClientTls} host={connection.HostName}"); return default; }

    public override async ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
    {
        Write($"Connecting {connection.ClientAddress} proxy={connection.Proxy.Name} host={connection.HostName} listen={connection.ListenPort == connection.Proxy.ListenPort}");
        await Misbehave(cancellationToken);
        return _denyClients.Contains(connection.ClientAddress) ? NpVerdict.Deny("blocked") : NpVerdict.Allow;
    }

    public override ValueTask<NpVerdict> OnEndpointConnectingAsync(INpConnection connection, NpEndpointInfo endpoint, CancellationToken cancellationToken)
    {
        Write($"EndpointConnecting {endpoint.Port}");
        return ValueTask.FromResult(endpoint.Port == _denyEndpointPort ? NpVerdict.Deny("skip") : NpVerdict.Allow);
    }

    public override ValueTask OnEndpointConnectedAsync(INpConnection connection, NpEndpointInfo endpoint, CancellationToken cancellationToken)
    { Write($"EndpointConnected {endpoint.Port}"); return default; }

    public override ValueTask OnEndpointConnectFailedAsync(INpConnection connection, NpEndpointInfo endpoint, string error, CancellationToken cancellationToken)
    { Write($"EndpointFailed {endpoint.Port}"); return default; }

    public override ValueTask OnConnectedAsync(INpConnection connection, CancellationToken cancellationToken)
    { Write($"Connected endpoint={connection.Endpoint?.Port}"); return default; }

    public override ValueTask OnIdleTimeoutAsync(INpConnection connection, CancellationToken cancellationToken) { Write("Idle"); return default; }

    public override ValueTask OnDisconnectingAsync(INpConnection connection, NpDisconnectReason reason, CancellationToken cancellationToken)
    { Write($"Disconnecting reason={reason} open={connection.Endpoint != null}"); return default; }

    public override ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
    { Write($"Disconnected reason={info.Reason} in={info.BytesFromClient} out={info.BytesFromEndpoint}"); return default; }

    public override async ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(data.Data.Span);
        Write($"DataReceived {data.Direction} enc={data.ReceivedEncrypted}->{data.WillBeSentEncrypted} {text.Replace("\r\n", " ")}");

        if (_closeOn != null && text.Contains(_closeOn))
        {
            data.CloseConnection("matched");
        }
        else if (_dropOn != null && text.Contains(_dropOn))
        {
            data.Drop();
        }

        await Misbehave(cancellationToken);
    }

    public override ValueTask OnDataSendingAsync(NpDataEventArgs data, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(data.Data.Span);
        Write($"DataSending {data.Direction} enc={data.ReceivedEncrypted}->{data.WillBeSentEncrypted} {text.Replace("\r\n", " ")}");

        if (_find != null && text.Contains(_find))
        {
            data.Replace(Encoding.UTF8.GetBytes(text.Replace(_find, _replace)));
        }

        return default;
    }

    public override ValueTask OnDataSentAsync(INpConnection connection, NpDataDirection direction, int byteCount, CancellationToken cancellationToken)
    { Write($"DataSent {direction} {byteCount}"); return default; }
}

[NpPlugin("Future", ApiVersion = 999)]
public class FuturePlugin : NpPlugin
{
}

[NpPlugin("Quiet")]
public class QuietPlugin : NpPlugin
{
}

public abstract class AbstractPlugin : NpPlugin
{
}

public class NotAPlugin
{
}
