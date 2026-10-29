using NetProxy.PluginApi;

namespace NetProxy.Service.Plugins
{
    /// <summary>
    /// The plug-ins that apply to something (the service, a proxy, a connection), in the order that they are called in, and the
    /// calls to them. A plug-in that does not override an event is not called for it.
    /// </summary>
    internal sealed class NpPluginSet
    {
        public static readonly NpPluginSet Empty = new([]);

        private readonly NpLoadedPlugin[] _plugins;

        public NpPluginSet(IEnumerable<NpLoadedPlugin> plugins)
        {
            _plugins = [.. plugins];

            HasDataHooks = _plugins.Any(o => o.Handles(nameof(NpPlugin.OnDataReceivedAsync)) || o.Handles(nameof(NpPlugin.OnDataSendingAsync)));
            HasDataSentHook = _plugins.Any(o => o.Handles(nameof(NpPlugin.OnDataSentAsync)));
        }

        public bool IsEmpty => _plugins.Length == 0;
        public bool HasDataHooks { get; }
        public bool HasDataSentHook { get; }
        public IReadOnlyList<NpLoadedPlugin> Plugins => _plugins;

        private async ValueTask NotifyAsync(string hook, Func<NpPlugin, CancellationToken, ValueTask> call)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(hook))
                {
                    await plugin.RunAsync(hook, call);
                }
            }
        }

        /// <summary>
        /// Asks the plug-ins in turn. The first one that says no ends it, and its answer includes who said it.
        /// </summary>
        private async ValueTask<NpVerdict> AskAsync(string hook, Func<NpPlugin, CancellationToken, ValueTask<NpVerdict>> call)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(hook) == false)
                {
                    continue;
                }

                var (succeeded, verdict) = await plugin.RunAsync(hook, call);
                if (succeeded && verdict.Allowed == false)
                {
                    return NpVerdict.Deny($"Plugin '{plugin.Configuration.Name}': {verdict.Reason ?? "not allowed"}");
                }
            }

            return NpVerdict.Allow;
        }

        //---------------------------------------------------------------- Service

        public async ValueTask TickAsync(NpLoadedPlugin plugin)
            => await plugin.RunAsync(nameof(NpPlugin.OnTickAsync), (p, ct) => p.OnTickAsync(ct));

        public ValueTask ManagementLoginAsync(NpLoginInfo login)
            => NotifyAsync(nameof(NpPlugin.OnManagementLoginAsync), (p, ct) => p.OnManagementLoginAsync(login, ct));

        public ValueTask ErrorAsync(NpPluginError error)
            => NotifyAsync(nameof(NpPlugin.OnErrorAsync), (p, ct) => p.OnErrorAsync(error, ct));

        //---------------------------------------------------------------- Proxy

        public ValueTask<NpVerdict> ProxyStartingAsync(NpProxyInfo proxy)
            => AskAsync(nameof(NpPlugin.OnProxyStartingAsync), (p, ct) => p.OnProxyStartingAsync(proxy, ct));

        public ValueTask ProxyStartedAsync(NpProxyInfo proxy, IReadOnlyList<string> listeningOn)
            => NotifyAsync(nameof(NpPlugin.OnProxyStartedAsync), (p, ct) => p.OnProxyStartedAsync(proxy, listeningOn, ct));

        public ValueTask ProxyStartFailedAsync(NpProxyInfo proxy, string error)
            => NotifyAsync(nameof(NpPlugin.OnProxyStartFailedAsync), (p, ct) => p.OnProxyStartFailedAsync(proxy, error, ct));

        public ValueTask ProxyStoppingAsync(NpProxyInfo proxy)
            => NotifyAsync(nameof(NpPlugin.OnProxyStoppingAsync), (p, ct) => p.OnProxyStoppingAsync(proxy, ct));

        public ValueTask ProxyStoppedAsync(NpProxyInfo proxy)
            => NotifyAsync(nameof(NpPlugin.OnProxyStoppedAsync), (p, ct) => p.OnProxyStoppedAsync(proxy, ct));

        public ValueTask ProxyConfigurationChangedAsync(NpProxyInfo proxy, NpChangeKind change)
            => NotifyAsync(nameof(NpPlugin.OnProxyConfigurationChangedAsync), (p, ct) => p.OnProxyConfigurationChangedAsync(proxy, change, ct));

        //---------------------------------------------------------------- Connection

        public ValueTask ConnectionRefusedAsync(NpProxyInfo proxy, string clientAddress, string reason)
            => NotifyAsync(nameof(NpPlugin.OnConnectionRefusedAsync), (p, ct) => p.OnConnectionRefusedAsync(proxy, clientAddress, reason, ct));

        public ValueTask ClientTlsEstablishedAsync(INpConnection connection)
            => NotifyAsync(nameof(NpPlugin.OnClientTlsEstablishedAsync), (p, ct) => p.OnClientTlsEstablishedAsync(connection, ct));

        public ValueTask<NpVerdict> ConnectingAsync(INpConnection connection)
            => AskAsync(nameof(NpPlugin.OnConnectingAsync), (p, ct) => p.OnConnectingAsync(connection, ct));

        /// <summary>
        /// Gives the endpoints to each plug-in in turn, so that each can change the order the one before it left.
        /// </summary>
        public async ValueTask EndpointsOrderingAsync(NpEndpointOrderEventArgs order)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(nameof(NpPlugin.OnEndpointsOrderingAsync)))
                {
                    await plugin.RunAsync(nameof(NpPlugin.OnEndpointsOrderingAsync), (p, ct) => p.OnEndpointsOrderingAsync(order, ct));
                }
            }
        }

        public ValueTask<NpVerdict> EndpointConnectingAsync(INpConnection connection, NpEndpointInfo endpoint)
            => AskAsync(nameof(NpPlugin.OnEndpointConnectingAsync), (p, ct) => p.OnEndpointConnectingAsync(connection, endpoint, ct));

        public ValueTask EndpointConnectedAsync(INpConnection connection, NpEndpointInfo endpoint)
            => NotifyAsync(nameof(NpPlugin.OnEndpointConnectedAsync), (p, ct) => p.OnEndpointConnectedAsync(connection, endpoint, ct));

        public ValueTask EndpointConnectFailedAsync(INpConnection connection, NpEndpointInfo endpoint, string error)
            => NotifyAsync(nameof(NpPlugin.OnEndpointConnectFailedAsync), (p, ct) => p.OnEndpointConnectFailedAsync(connection, endpoint, error, ct));

        /// <summary>
        /// Asks each plug-in in turn until one answers the client.
        /// </summary>
        public async ValueTask EndpointsUnavailableAsync(NpUnavailableEventArgs unavailable)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(nameof(NpPlugin.OnEndpointsUnavailableAsync)))
                {
                    await plugin.RunAsync(nameof(NpPlugin.OnEndpointsUnavailableAsync), (p, ct) => p.OnEndpointsUnavailableAsync(unavailable, ct));

                    if (unavailable.Response != null)
                    {
                        return;
                    }
                }
            }
        }

        public ValueTask ConnectedAsync(INpConnection connection)
            => NotifyAsync(nameof(NpPlugin.OnConnectedAsync), (p, ct) => p.OnConnectedAsync(connection, ct));

        public ValueTask IdleTimeoutAsync(INpConnection connection)
            => NotifyAsync(nameof(NpPlugin.OnIdleTimeoutAsync), (p, ct) => p.OnIdleTimeoutAsync(connection, ct));

        public ValueTask DisconnectingAsync(INpConnection connection, NpDisconnectReason reason)
            => NotifyAsync(nameof(NpPlugin.OnDisconnectingAsync), (p, ct) => p.OnDisconnectingAsync(connection, reason, ct));

        public ValueTask DisconnectedAsync(INpConnection connection, NpDisconnectInfo info)
            => NotifyAsync(nameof(NpPlugin.OnDisconnectedAsync), (p, ct) => p.OnDisconnectedAsync(connection, info, ct));

        //---------------------------------------------------------------- Data

        /// <summary>
        /// Gives the data to each plug-in in turn. A plug-in that changes it hands the changed data to the next. Stops as soon as the
        /// data is dropped or the connection is to be closed.
        /// </summary>
        public async ValueTask DataReceivedAsync(NpDataEventArgs data)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(nameof(NpPlugin.OnDataReceivedAsync)))
                {
                    await plugin.RunAsync(nameof(NpPlugin.OnDataReceivedAsync), (p, ct) => p.OnDataReceivedAsync(data, ct));

                    if (data.IsDropped)
                    {
                        return;
                    }
                }
            }
        }

        public async ValueTask DataSendingAsync(NpDataEventArgs data)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.Handles(nameof(NpPlugin.OnDataSendingAsync)))
                {
                    await plugin.RunAsync(nameof(NpPlugin.OnDataSendingAsync), (p, ct) => p.OnDataSendingAsync(data, ct));

                    if (data.IsDropped)
                    {
                        return;
                    }
                }
            }
        }

        public ValueTask DataSentAsync(INpConnection connection, NpDataDirection direction, int byteCount)
            => NotifyAsync(nameof(NpPlugin.OnDataSentAsync), (p, ct) => p.OnDataSentAsync(connection, direction, byteCount, ct));
    }
}
