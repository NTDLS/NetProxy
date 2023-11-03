using NetProxy.Library.Utilities;

namespace NetProxy.Service
{
    public static class Singletons
    {
        private static Logging? _eventLog = null;
        public static Logging EventLog
        {
            get
            {
                _eventLog ??= new Logging(false);
                return _eventLog;
            }
        }
    }
}
