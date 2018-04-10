using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetProxy.Library.Win32;

namespace NetProxy.Service
{
    public static class Singletons
    {
        private static EventLogging eventLog = null;
        public static EventLogging EventLog
        {
            get
            {
                if (eventLog == null)
                {
                    eventLog = new EventLogging(Library.Constants.TitleCaption, false);
                }

                return eventLog;
            }
        }
    }
}
