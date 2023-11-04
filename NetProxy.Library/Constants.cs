namespace NetProxy.Library
{
    public static class Constants
    {
        public static class CommandLables
        {
            public const string GuiRequestRouteList = "GUIRequestRouteList";
            public const string GuiRequestRoute = "GUIRequestRoute";
            public const string GuiRequestLogin = "GUIRequestLogin";
            public const string GuiRequestUserList = "GUIRequestUserList";
            public const string GuiPersistUserList = "GUIPersistUserList";
            public const string GuiPersistUpsertRoute = "GUIPersistUpsertRoute";
            public const string GuiPersistDeleteRoute = "GUIPersistDeleteRoute";
            public const string GuiPersistStopRoute = "GUIPersistStopRoute";
            public const string GuiPersistStartRoute = "GUIPersistStartRoute";
            public const string GuiSendMessage = "GUISendMessage";
            public const string GuiRequestRouteStatsList = "GUIRequestRouteStatsList";
        }

        public const string TitleCaption = "NetProxy";
        public const int DefaultManagementPort = 5854;
        public const int DefaultInitialBufferSize = 4096;
        public const int DefaultMaxBufferSize = 1048576;
        public const int DefaultStickySessionExpiration = 3600;
        public const int DefaultSpinLockCount = 1000000;
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
        Binary,
        Http,
        Https
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
        DoubleCrlf,
        DoubleLf
    }
}
