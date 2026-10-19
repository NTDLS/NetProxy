using System.Runtime.Serialization;

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
        public const int DefaultAcceptBacklogSize = 10;
        public const int DefaultLogFileMaxSizeMB = 10;
        public const int DefaultLogFileRetainedCount = 5;
        public const string EventSourceName = "NetProxy";
    }

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
    /// The serialized names of the first two are kept ("Pv4", "Pv6") so that existing configuration files still load.
    /// </summary>
    public enum BindingProtocol
    {
        [EnumMember(Value = "Pv4")]
        IPv4,
        [EnumMember(Value = "Pv6")]
        IPv6,
        /// <summary>
        /// One dual-stack listener that accepts both IPv6 and IPv4 clients.
        /// </summary>
        Both
    }

    public enum TrafficType
    {
        Raw,
        Http,
        Https
    }

    public enum ConnectionPattern
    {
        FailOver,
        RoundRobbin,
        Balanced //AKA: Least connections.
    }

    public enum HttpHeaderAction
    {
        Insert,
        Update,
        Delete,
        Upsert
    }

    public enum HttpHeaderType
    {
        None,
        Request,
        Response,
        Any
    }
}
