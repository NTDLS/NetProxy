namespace NetProxy.Library
{
    public static class Constants
    {
        public static class CommandLables
        {
            public const string GUIRequestRouteList = "GUIRequestRouteList";
            public const string GUIRequestRoute = "GUIRequestRoute";
            public const string GUIRequestLogin = "GUIRequestLogin";
            public const string GUIRequestUserList = "GUIRequestUserList";
            public const string GUIPersistUserList = "GUIPersistUserList";
            public const string GUIPersistUpsertRoute = "GUIPersistUpsertRoute";
            public const string GUIPersistDeleteRoute = "GUIPersistDeleteRoute";
            public const string GUIPersistStopRoute = "GUIPersistStopRoute";
            public const string GUIPersistStartRoute = "GUIPersistStartRoute";
            public const string GUISendMessage = "GUISendMessage";
            public const string GUIRequestRouteStatsList = "GUIRequestRouteStatsList";
        }

        public static class IntraServiceLables
        {
            public const string ApplyNegotiationToken = "ApplyNegotiationToken";
            public const string ApplyResponseNegotiationToken = "ApplyResponseNegotiationToken";
            public const string EncryptionNegotationComplete = "EncryptionNegotationComplete";
            public const string TunnelNegotationComplete = "TunnelNegotationComplete";
        }

        public const string RegsitryKey = "SOFTWARE\\NetworkDLS\\NetProxy";
        public const string ServiceName = "NetworkDLSNetProxyService";
        public const string TitleCaption = "NetProxy";
        public const int DEFAULT_MANAGEMENT_PORT = 5854;
        public const int DEFAULT_INITIAL_BUFFER_SIZE = 4096;
        public const int DEFAULT_MAX_BUFFER_SIZE = 1048576;
        public const int DEFAULT_STICKY_SESSION_EXPIRATION = 3600;
        public const int DEFAULT_SPIN_LOCK_COUNT = 1000000;
        public const int DEFAULT_ENCRYPTION_INITILIZATION_TIMEOUT_MS = 10000;
        public const int DEFAULT_ACCEPT_BACKLOG_SIZE = 10;
        public const string ROUTES_CONFIG_FILE_NAME = "routes.json";
        public const string SERVER_CONFIG_FILE_NAME = "service.json";
    }

    public enum HTTPVerb
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

    public enum BindingProtocal
    {
        IPv4,
        IPv6
    }

    public enum TrafficType
    {
        Binary,
        HTTP,
        HTTPS
    }

    public enum ConnectionPattern
    {
        FailOver,
        RoundRobbin,
        Balanced
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

    public enum HttpHeaderLineBreakType
    {
        None,
        DoubleCRLF,
        DoubleLF
    }
}
