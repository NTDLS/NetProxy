﻿using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NetProxy.Library.Utilities
{
    public class Logging
    {
        public enum Severity
        {
            Verbose = 0,
            Information = 1,
            Warning = 2,
            Error = 3
        }

        public class LoggingPayload
        {
            public Severity Severity { get; set; }
            public Exception? Exception { get; set; }
            public string CustomText { get; set; } = string.Empty;
            public object UserData { get; set; } = string.Empty;
        }

        public bool WriteVerboseLogging { get; set; }

        public Logging(bool writeVerboseLogging)
        {
            WriteVerboseLogging = writeVerboseLogging;
        }

        public void WriteLog(LoggingPayload payload)
        {
            try
            {
                System.Text.StringBuilder errorMessage = new System.Text.StringBuilder();

                if ((string)Utility.IsNull(payload.CustomText, string.Empty) != string.Empty)
                {
                    errorMessage.AppendFormat("{0}\r\n", payload.CustomText);
                }

                if (payload.Exception != null)
                {
                    string exceptionMessage = Utility.GetExceptionText(payload.Exception);

                    //If we do not have a message, then we have to find something to report on.
                    if (exceptionMessage == null || exceptionMessage == string.Empty)
                    {
                        if (payload.Exception.HResult != 0)
                        {
                            Exception? hresultEx = null;

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
                                exceptionMessage = string.Format("{0}\r\n", Utility.GetExceptionText(hresultEx));
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
                    MethodBase? methodBase = stackTrace?.GetFrame(1)?.GetMethod();
                    errorMessage.AppendFormat("Calling Method: {0}\r\n", methodBase?.Name ?? "");

                    if (payload.Exception != null && payload.Exception.StackTrace != null)
                    {
                        errorMessage.AppendFormat("Stack: {0}\r\n", payload.Exception.StackTrace);
                    }
                }

                WriteLog(payload.Severity, errorMessage.ToString());
            }
            catch
            {
                //Discard error - we don't want a failure to log a verbose event to cause service failure.
            }
        }

        public void WriteLog(Severity severity, string eventText)
        {
            try
            {
                Console.WriteLine($"<{severity}> {eventText}");

                if (severity == Severity.Verbose && !WriteVerboseLogging)
                {
                    return;
                }

                //TODO: Write log to file...
                //EventLog.WriteEntry(this.ApplicationName, eventText, eventLogEntryType);
            }
            catch
            {
                //Discard error - we don't want a failure to log a verbose event to cause service failure.
            }
        }
    }
}