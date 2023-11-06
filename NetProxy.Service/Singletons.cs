using NetProxy.Library.Utilities;

namespace NetProxy.Service
{
    public static class Singletons
    {
        private static NpLogging? _eventLog = null;
        public static NpLogging Logging
        {
            get
            {
                _eventLog ??= new NpLogging(false);
                return _eventLog;
            }
        }
    }
}
