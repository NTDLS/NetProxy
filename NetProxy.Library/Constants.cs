namespace NetProxy.Library
{
    public static class Constants
    {
        public const string TitleCaption = "NetProxy";
        public const int DefaultManagementPort = 5854;
        public const int DefaultInitialBufferSize = 10 * 1024;
        public const int DefaultMaxBufferSize = 64 * 1024;
        public const int DefaultStickySessionExpiration = 60 * 60;
        public const int DefaultAcceptBacklogSize = 10;
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
        Put
    }

    public enum ConnectionDirection
    {
        Undefined,
        Inbound,
        Outbound
    }

    public enum BindingProtocol
    {
        Pv4,
        Pv6
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
