using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using NetProxy.Library.Utility;

namespace NetProxy.Library.Win32
{
    public class EventLogging
    {
        public enum Severity
        {
            Verbose = 0, //Not written to the event log unless (_WriteVerboseLogging==true).
            Information = 1,
            Warning = 2,
            Error = 3
        }

        public class EventPayload
        {
            public Severity Severity { get; set; }
            public Exception Exception { get; set; }
            public string CustomText { get; set; }
            public object UserData { get; set; }
        }

        public string ApplicationName { get; set; }

        private bool _writeVerboseLogging;

        public bool WriteVerboseLogging
        {
            get
            {
                return _writeVerboseLogging;
            }
            set
            {
                _writeVerboseLogging = value;
            }
        }

        public EventLogging(string applicationName, bool writeVerboseLogging)
        {
            this._writeVerboseLogging = writeVerboseLogging;
            this.ApplicationName = applicationName;

            //This is required before events can be written.
            if (!EventLog.SourceExists(this.ApplicationName))
            {
                EventLog.CreateEventSource(this.ApplicationName, "Application");
            }
        }

        public void WriteEvent(EventPayload payload)
        {
            try
            {
                System.Text.StringBuilder errorMessage = new System.Text.StringBuilder();

                if (((string)Helpers.IsNull(payload.CustomText, string.Empty)) != string.Empty)
                {
                    errorMessage.AppendFormat("{0}\r\n", payload.CustomText);
                }

                if (payload.Exception != null)
                {
                    string exceptionMessage = Exceptions.GetExceptionText(payload.Exception);

                    //If we do not have a message, then we have to find something to report on.
                    if (exceptionMessage == null || exceptionMessage == string.Empty)
                    {
                        if (payload.Exception.HResult != 0)
                        {
                            Exception hresultEx = null;

                            try
                            {
                                hresultEx = Marshal.GetExceptionForHR(payload.Exception.HResult);
                            }
                            catch
                            {
                                //throw away...
                            }

                            if (hresultEx != null)
                            {
                                exceptionMessage = string.Format("{0}\r\n", Exceptions.GetExceptionText(hresultEx));
                            }
                            else
                            {
                                exceptionMessage = string.Format("HResult: {0}\r\n", payload.Exception.HResult.ToString());
                            }
                        }
                    }

                    if (exceptionMessage != null && exceptionMessage != string.Empty)
                    {
                        errorMessage.AppendFormat("Exception: {0}\r\n", exceptionMessage);
                    }
                }

                if (payload.Exception != null && payload.Severity == Severity.Error)
                {
                    StackTrace stackTrace = new StackTrace();
                    MethodBase methodBase = stackTrace.GetFrame(1).GetMethod();
                    errorMessage.AppendFormat("Calling Method: {0}\r\n", methodBase.Name);

                    if (payload.Exception != null && payload.Exception.StackTrace != null)
                    {
                        errorMessage.AppendFormat("Stack: {0}\r\n", payload.Exception.StackTrace);
                    }
                }

                this.WriteEvent(payload.Severity, errorMessage.ToString());
            }
            catch
            {
                //Discard error - we don't want a failure to log a verbose event to cause service failure.
            }
        }

        public void WriteEvent(Severity severity, string eventText)
        {
            try
            {
                Console.WriteLine(string.Format("<{0}> {1}", severity.ToString(), eventText));

                if (severity == Severity.Verbose && !_writeVerboseLogging)
                {
                    return;
                }

                EventLogEntryType eventLogEntryType = EventLogEntryType.Information;
                switch (severity)
                {
                    case Severity.Verbose:
                        eventLogEntryType = EventLogEntryType.Information;
                        break;
                    case Severity.Information:
                        eventLogEntryType = EventLogEntryType.Information;
                        break;
                    case Severity.Warning:
                        eventLogEntryType = EventLogEntryType.Warning;
                        break;
                    case Severity.Error:
                        eventLogEntryType = EventLogEntryType.Error;
                        break;
                }

                EventLog.WriteEntry(this.ApplicationName, eventText, eventLogEntryType);
            }
            catch
            {
                //Discard error - we don't want a failure to log a verbose event to cause service failure.
            }
        }
    }
}