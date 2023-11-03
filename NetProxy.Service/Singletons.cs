using NetProxy.Library.Utility;

namespace NetProxy.Service
{
    public static class Singletons
    {
        private static Logging _eventLog = null;
        public static Logging EventLog
        {
            get
            {
                if (_eventLog == null)
                {
                    _eventLog = new Logging(Library.Constants.TitleCaption, false);
                }

                return _eventLog;
            }
        }
    }
}
