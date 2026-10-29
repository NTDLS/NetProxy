using System.ComponentModel;

namespace NetProxy.PluginApi
{
    /// <summary>
    /// The base class of a NetProxy plug-in. Derive from it (the class needs a constructor without parameters), mark it with
    /// <see cref="NpPluginAttribute"/>, and override the events that you want. Every event does nothing by default, and the service
    /// only calls the ones that you override.
    ///
    /// The plug-ins that are enabled for a proxy are called in the order they have in the service configuration. An event that
    /// can be vetoed stops at the first plug-in that says no. For the data events each plug-in gets the data as the plug-in before
    /// it left it.
    ///
    /// A plug-in that throws, or takes longer than its time limit, is logged and skipped for that event. It does not stop the connection.
    /// Events for connections run on the connection, so keep them quick, and keep the data events quickest of all.
    /// </summary>
    public abstract class NpPlugin
    {
        /// <summary>
        /// The service. Available from <see cref="OnServiceStartingAsync"/> on.
        /// </summary>
        protected INpPluginHost Host { get; private set; } = null!;

        /// <summary>
        /// Called by the service. Not for plug-ins.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Attach(INpPluginHost host) => Host = host;

        /// <summary>
        /// How often <see cref="OnTickAsync"/> is called. Null (the default) for never.
        /// </summary>
        public virtual TimeSpan? TickInterval => null;

        //---------------------------------------------------------------- Service (every enabled plug-in)

        /// <summary>The service has loaded the plug-in. Set up here.</summary>
        public virtual ValueTask OnServiceStartingAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>The service is stopping, or the plug-in is being unloaded. Clean up here.</summary>
        public virtual ValueTask OnServiceStoppingAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>Called every <see cref="TickInterval"/>.</summary>
        public virtual ValueTask OnTickAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>Someone logged in to the management interface, or tried to.</summary>
        public virtual ValueTask OnManagementLoginAsync(NpLoginInfo login, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>Something failed in the service or on a connection.</summary>
        public virtual ValueTask OnErrorAsync(NpPluginError error, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        //---------------------------------------------------------------- Proxy (plug-ins enabled for the proxy)

        /// <summary>The proxy is about to start listening. Deny to keep it from starting.</summary>
        public virtual ValueTask<NpVerdict> OnProxyStartingAsync(NpProxyInfo proxy, CancellationToken cancellationToken)
            => ValueTask.FromResult(NpVerdict.Allow);

        /// <summary>The proxy is listening. <paramref name="listeningOn"/> describes the addresses and ports.</summary>
        public virtual ValueTask OnProxyStartedAsync(NpProxyInfo proxy, IReadOnlyList<string> listeningOn, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>The proxy could not start (for example the port is in use, or a plug-in said no).</summary>
        public virtual ValueTask OnProxyStartFailedAsync(NpProxyInfo proxy, string error, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>The proxy is stopping. Its connections are still open.</summary>
        public virtual ValueTask OnProxyStoppingAsync(NpProxyInfo proxy, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>The proxy has stopped and its connections are closed.</summary>
        public virtual ValueTask OnProxyStoppedAsync(NpProxyInfo proxy, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>The configuration of the proxy was added, changed or deleted.</summary>
        public virtual ValueTask OnProxyConfigurationChangedAsync(NpProxyInfo proxy, NpChangeKind change, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        //---------------------------------------------------------------- Connection (plug-ins enabled for the proxy)

        /// <summary>A client was turned away because the proxy is at one of its connection limits.</summary>
        public virtual ValueTask OnConnectionRefusedAsync(NpProxyInfo proxy, string clientAddress, string reason, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>
        /// The client has completed the TLS handshake (the proxy terminates TLS). <see cref="INpConnection.HostName"/> is the server
        /// name it asked for, and <see cref="INpConnection.ClientTls"/> the protocol.
        /// </summary>
        public virtual ValueTask OnClientTlsEstablishedAsync(INpConnection connection, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>
        /// A client has connected and the proxy it is for is known, but it is not connected to an endpoint yet. Deny to close it.
        /// </summary>
        public virtual ValueTask<NpVerdict> OnConnectingAsync(INpConnection connection, CancellationToken cancellationToken)
            => ValueTask.FromResult(NpVerdict.Allow);

        /// <summary>
        /// The endpoints that the client is to be connected to are known, in the order they will be tried. Change that order with
        /// <see cref="NpEndpointOrderEventArgs.Prefer"/> (a canary release, say). Called for TCP, HTTP and HTTPS proxies.
        /// </summary>
        public virtual ValueTask OnEndpointsOrderingAsync(NpEndpointOrderEventArgs order, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>The proxy is about to connect to an endpoint. Deny to skip this endpoint (the next one is tried).</summary>
        public virtual ValueTask<NpVerdict> OnEndpointConnectingAsync(INpConnection connection, NpEndpointInfo endpoint, CancellationToken cancellationToken)
            => ValueTask.FromResult(NpVerdict.Allow);

        public virtual ValueTask OnEndpointConnectedAsync(INpConnection connection, NpEndpointInfo endpoint, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public virtual ValueTask OnEndpointConnectFailedAsync(INpConnection connection, NpEndpointInfo endpoint, string error, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>
        /// Every endpoint has failed or been skipped, so the client can not be connected to anything. Answer it with
        /// <see cref="NpUnavailableEventArgs.Respond"/> (a page that tells that the site is down for maintenance, say). The plug-ins that
        /// are enabled for the proxy are asked in order until one answers. Without an answer the connection is closed.
        /// </summary>
        public virtual ValueTask OnEndpointsUnavailableAsync(NpUnavailableEventArgs unavailable, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>The client is connected to an endpoint and data starts to flow.</summary>
        public virtual ValueTask OnConnectedAsync(INpConnection connection, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>The connection has had no data in either direction for the idle timeout of the proxy and is about to be closed.</summary>
        public virtual ValueTask OnIdleTimeoutAsync(INpConnection connection, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>The connection is ending. Its sockets are still open.</summary>
        public virtual ValueTask OnDisconnectingAsync(INpConnection connection, NpDisconnectReason reason, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        /// <summary>The connection is closed.</summary>
        public virtual ValueTask OnDisconnectedAsync(INpConnection connection, NpDisconnectInfo info, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        //---------------------------------------------------------------- Data (plug-ins enabled for the proxy)

        /// <summary>
        /// Data was read from one side, before the proxy does anything with it (before the HTTP header rules). For a proxy that
        /// terminates TLS this is the decrypted data. Change it with <see cref="NpDataEventArgs.Replace"/>, drop it, close the connection,
        /// or answer the client yourself with <see cref="NpDataEventArgs.Respond"/> (see <see cref="NpDataEventArgs.CanRespond"/>).
        /// A read is whatever arrived together: it is not a message, so a request or a record can be split over several reads.
        /// </summary>
        public virtual ValueTask OnDataReceivedAsync(NpDataEventArgs data, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>
        /// Data is about to be written to the other side, after the proxy has applied its HTTP header rules. For a connection that
        /// is TLS on the other side, it is encrypted after this. Change it with <see cref="NpDataEventArgs.Replace"/>, drop it, or close the connection.
        /// </summary>
        public virtual ValueTask OnDataSendingAsync(NpDataEventArgs data, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <summary>Data was written to the other side.</summary>
        public virtual ValueTask OnDataSentAsync(INpConnection connection, NpDataDirection direction, int byteCount, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
