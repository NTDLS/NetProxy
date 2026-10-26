
namespace NetProxy.Library
{
    public static class Constants
    {
        public const string FriendlyName = "NetProxy";
        public const int DefaultManagementPort = 5854;

        /// <summary>
        /// The size, in bits, of the key that a management connection is encrypted with.
        /// </summary>
        public const int ManagementCryptographyKeySize = 4096;
        public const int DefaultInitialBufferSize = 10 * 1024;
        public const int DefaultMaxBufferSize = 64 * 1024;
        public const int DefaultStickySessionExpiration = 60 * 60;
        public const int DefaultPluginTimeoutMilliseconds = 5000;
        /// <summary>How long a UDP session is kept without datagrams when the proxy has no idle timeout of its own (seconds).</summary>
        public const int DefaultUdpIdleTimeoutSeconds = 60;
        /// <summary>The most a UDP datagram can carry (the largest the protocol allows).</summary>
        public const int UdpMaxDatagramSize = 65535;
        public const int MinAcceptBacklogSize = 128;
        public const int DefaultAcceptBacklogSize = MinAcceptBacklogSize;
        public const int DefaultLogFileMaxSizeMB = 10;
        /// <summary>How often the statistics of the proxies are written to disk (seconds). They are also written when the service stops.</summary>
        public const int StatisticsSaveIntervalSeconds = 120;
        public const int DefaultLogFileRetainedCount = 5;
        public const string EventSourceName = "NetProxy";
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum HttpVerb
    {
        Any,
        Connect,
        Delete,
        Get,
        Head,
        Options,
        Post,
        Put,
        Patch,
        Trace
    }

    public enum ConnectionDirection
    {
        Inbound,
        Outbound
    }

    /// <summary>
    /// Which IP versions a proxy listens on when it listens on all addresses. (Explicit bindings use the version of the address.)
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum BindingProtocol
    {
        IPv4,
        IPv6,
        /// <summary>
        /// One dual-stack listener that accepts both IPv6 and IPv4 clients.
        /// </summary>
        Both
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum TrafficType
    {
        Tcp,
        Http,
        Https,
        /// <summary>UDP datagrams, relayed between sessions that are kept for each client address and port.</summary>
        Udp
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum ConnectionPattern
    {
        FailOver,
        RoundRobbin,
        Balanced //AKA: Least connections.
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum ForwardedHeadersMode
    {
        Off,
        /// <summary>X-Forwarded-For and X-Forwarded-Proto are set to the client, replacing what the client sent.</summary>
        Replace,
        /// <summary>The client is added to the end of X-Forwarded-For. For a proxy behind another proxy.</summary>
        Append
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum HttpHeaderAction
    {
        Insert,
        Update,
        Delete,
        Upsert
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum HttpHeaderType
    {
        None,
        Request,
        Response,
        Any
    }
}
