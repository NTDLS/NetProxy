namespace NetProxy.PluginApi
{
    /// <summary>
    /// The version of this API. A plug-in built for a newer version than the service has is not loaded. New events are added
    /// to <see cref="NpPlugin"/> as virtual methods that do nothing, so plug-ins keep working when the API grows.
    /// </summary>
    public static class NpPluginApiInfo
    {
        public const int Version = 1;
    }

    /// <summary>
    /// Describes a plug-in class. Put it on the class that derives from <see cref="NpPlugin"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class NpPluginAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
        public string Version { get; set; } = "1.0";
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// The version of the plug-in API that the plug-in was written for.
        /// </summary>
        public int ApiVersion { get; set; } = NpPluginApiInfo.Version;
    }

    public enum NpPluginLogLevel { Verbose, Information, Warning, Error }

    /// <summary>
    /// What the service offers to a plug-in.
    /// </summary>
    public interface INpPluginHost
    {
        /// <summary>
        /// The name that the plug-in has been given in the service configuration.
        /// </summary>
        string InstanceName { get; }

        /// <summary>
        /// The settings of the plug-in, as entered in the management console (name and value pairs).
        /// </summary>
        IReadOnlyDictionary<string, string> Settings { get; }

        /// <summary>
        /// A folder, on the machine that the service runs on, that is private to this plug-in. It exists.
        /// </summary>
        string DataDirectory { get; }

        /// <summary>
        /// Writes to the log of the service, as coming from this plug-in.
        /// </summary>
        void Log(NpPluginLogLevel level, string message);
    }

    public enum NpTrafficKind
    {
        Tcp,
        Http,
        Https,
        Udp,
    }

    public enum NpChangeKind { Added, Changed, Deleted }

    /// <summary>
    /// The way a data event is directed: the client is whoever connected to the proxy, the endpoint is where the proxy sends it.
    /// </summary>
    public enum NpDataDirection { ClientToEndpoint, EndpointToClient }

    public enum NpDisconnectReason
    {
        /// <summary>Both sides finished.</summary>
        Completed,
        IdleTimeout,
        /// <summary>The proxy was stopped, or the connection was aborted, or one side dropped it.</summary>
        Aborted,
        /// <summary>A plug-in (or the proxy limits) did not allow the connection.</summary>
        Denied,
        EndpointUnavailable,
        ClosedByPlugin,
        Error
    }

    /// <summary>
    /// The answer of a plug-in to an event that it can say no to.
    /// </summary>
    public readonly record struct NpVerdict(bool Allowed, string? Reason = null)
    {
        public static NpVerdict Allow => new(true);
        public static NpVerdict Deny(string reason) => new(false, reason);
    }

    public sealed record NpProxyInfo(Guid Id, string Name, int ListenPort, NpTrafficKind Traffic);

    public sealed record NpEndpointInfo(Guid Id, string Address, int Port, bool UsesTls);

    public sealed record NpDisconnectInfo(NpDisconnectReason Reason, TimeSpan Duration, long BytesFromClient, long BytesFromEndpoint);

    /// <summary>
    /// A failure that the service reports to the plug-ins.
    /// </summary>
    public sealed record NpPluginError(string Message, Exception? Exception, NpProxyInfo? Proxy, INpConnection? Connection);

    public sealed record NpLoginInfo(string UserName, bool Succeeded);

    /// <summary>
    /// A client connection and the connection to the endpoint that it is proxied to.
    /// </summary>
    public interface INpConnection
    {
        Guid Id { get; }
        NpProxyInfo Proxy { get; }
        DateTime StartedUtc { get; }

        string ClientAddress { get; }
        int ClientPort { get; }

        /// <summary>The local address and port that the client connected to.</summary>
        string ListenAddress { get; }
        int ListenPort { get; }

        /// <summary>
        /// The host name that the client asked for: the server name of the TLS handshake, or the Host header when the proxy
        /// shares its port with others. Null when it is not known.
        /// </summary>
        string? HostName { get; }

        /// <summary>The TLS version of the client side of the connection, or null when it is not encrypted.</summary>
        string? ClientTls { get; }
        string? EndpointTls { get; }
        bool IsHttp2 { get; }

        /// <summary>The endpoint that the connection is made to. Null until it is connected.</summary>
        NpEndpointInfo? Endpoint { get; }

        long BytesFromClient { get; }
        long BytesFromEndpoint { get; }

        /// <summary>
        /// Somewhere to keep what a plug-in needs to remember about this connection between events. Use a key that is yours.
        /// </summary>
        IDictionary<string, object?> State { get; }

        /// <summary>
        /// Ends the connection.
        /// </summary>
        void Close(string reason);
    }

    /// <summary>
    /// The data that is passing through the proxy, between the two sides of it. This is the data as the proxy handles it: for an
    /// HTTPS proxy that terminates TLS it is decrypted on the way in and is encrypted again (for the endpoint, if that uses TLS) on
    /// the way out. A TCP proxy that passes TLS through only has the encrypted bytes.
    /// </summary>
    public sealed class NpDataEventArgs(INpConnection connection, NpDataDirection direction, ReadOnlyMemory<byte> data,
        bool receivedEncrypted, bool willBeSentEncrypted, bool canRespond = false)
    {
        public INpConnection Connection { get; } = connection;
        public NpDataDirection Direction { get; } = direction;

        /// <summary>
        /// The data, as it is now. When an earlier plug-in has changed it, this is the changed data. It is only valid during the event.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; private set; } = data;

        /// <summary>True when the data was encrypted on the wire where it came from (and the proxy has decrypted it).</summary>
        public bool ReceivedEncrypted { get; } = receivedEncrypted;

        /// <summary>True when the proxy will encrypt the data to send it on.</summary>
        public bool WillBeSentEncrypted { get; } = willBeSentEncrypted;

        public bool IsModified { get; private set; }
        public bool IsDropped { get; private set; }
        public string? CloseReason { get; private set; }

        /// <summary>
        /// True when <see cref="Respond"/> can be used. That is for data that a client sent (<see cref="NpDataDirection.ClientToEndpoint"/>)
        /// in <see cref="NpPlugin.OnDataReceivedAsync"/>, on a TCP, HTTP or HTTPS proxy, and not when the client speaks HTTP/2.
        /// </summary>
        public bool CanRespond { get; } = canRespond;

        /// <summary>
        /// What the plug-in answered with instead of sending the data on, or null.
        /// </summary>
        public ReadOnlyMemory<byte>? Response { get; private set; }

        /// <summary>
        /// True when the connection is to be closed once <see cref="Response"/> has been sent.
        /// </summary>
        public bool CloseAfterResponse { get; private set; }

        /// <summary>
        /// Answers the client with these bytes, from the proxy, and does not send the data on: the endpoint never sees it. The connection
        /// stays open unless <paramref name="closeConnection"/> is set. Use it for a cached page, a page that tells that a site is down for
        /// maintenance, or a refusal.
        ///
        /// Mind that a read is not a message. The data may hold part of a request, or several. The plug-in has to know that it has the whole
        /// request, and that the endpoint has not got the start of it, or an answer for a request that is still being answered. The bytes
        /// are sent as they are: for HTTP that is a whole response, with its status line, headers and body.
        /// </summary>
        /// <exception cref="InvalidOperationException">When <see cref="CanRespond"/> is false.</exception>
        public void Respond(ReadOnlyMemory<byte> response, bool closeConnection = false)
        {
            if (CanRespond == false)
            {
                throw new InvalidOperationException("The client can not be answered from this event (see NpDataEventArgs.CanRespond).");
            }

            Response = response.ToArray(); //A copy, so that the plug-in can reuse its own buffer.
            CloseAfterResponse = closeConnection;
            Drop();
        }

        /// <summary>
        /// Sends this data instead of the data that was received. It can be longer or shorter.
        /// </summary>
        public void Replace(ReadOnlyMemory<byte> data)
        {
            Data = data.ToArray(); //A copy, so that the plug-in can reuse its own buffer.
            IsModified = true;
        }

        /// <summary>
        /// Does not send this data on. The connection stays open.
        /// </summary>
        public void Drop()
        {
            Data = ReadOnlyMemory<byte>.Empty;
            IsDropped = true;
        }

        /// <summary>
        /// Does not send this data on, and ends the connection.
        /// </summary>
        public void CloseConnection(string reason)
        {
            Drop();
            CloseReason = reason;
        }
    }
}
