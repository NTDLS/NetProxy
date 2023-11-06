namespace NetProxy.Library
{
    public static class Constants
    {
        public static class CommandLables
        {
            public const string GuiRequestProxyList = "GUIRequestProxyList";
            public const string GuiRequestProxy = "GUIRequestProxy";
            public const string GuiRequestLogin = "GUIRequestLogin";
            public const string GuiRequestUserList = "GUIRequestUserList";
            public const string GuiPersistUserList = "GUIPersistUserList";
            public const string GuiPersistUpsertProxy = "GUIPersistUpsertProxy";
            public const string GuiPersistDeleteProxy = "GUIPersistDeleteProxy";
            public const string GuiPersistStopProxy = "GUIPersistStopProxy";
            public const string GuiPersistStartProxy = "GUIPersistStartProxy";
            public const string GuiSendMessage = "GUISendMessage";
            public const string GuiRequestProxyStatsList = "GUIRequestProxyStatsList";
        }

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
        Undefiend,
        Inbound,
        Outbound
    }

    public enum BindingProtocal
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
