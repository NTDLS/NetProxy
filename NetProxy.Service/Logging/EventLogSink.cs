using NetProxy.Library;
using NetProxy.Library.Logging;
using NetProxy.Library.Utilities;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace NetProxy.Service.Logging
{
    /// <summary>
    /// Writes warnings and exceptions to the Windows Application event log, where administrators and
    ///     monitoring tools expect a service to report problems. Lower severities are not written.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class EventLogSink : ILogSink
    {
        /// <summary>
        /// The event log rejects messages longer than 31,839 characters.
        /// </summary>
        private const int MaxMessageLength = 31_000;

        private readonly EventLog? _eventLog;

        public bool IsEnabled { get; set; } = true;

        public EventLogSink()
        {
            try
            {
                //The source is normally created by the installer. Creating it here requires administrative rights,
                //  which the service has (LocalSystem) but an interactive debugging session might not.
                if (EventLog.SourceExists(Constants.EventSourceName) == false)
                {
                    EventLog.CreateEventSource(Constants.EventSourceName, "Application");
                }

                _eventLog = new EventLog("Application") { Source = Constants.EventSourceName };
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"NetProxy: Event log source '{Constants.EventSourceName}' is unavailable, event logging is disabled: {ex.Message}");
            }
        }

        public void Write(LogEntry entry)
        {
            if (_eventLog == null || IsEnabled == false)
            {
                return;
            }

            var entryType = entry.Severity switch
            {
                NpLogging.Severity.Exception => EventLogEntryType.Error,
                NpLogging.Severity.Warning => EventLogEntryType.Warning,
                _ => (EventLogEntryType?)null
            };

            if (entryType == null)
            {
                return;
            }

            var message = entry.Format();
            if (message.Length > MaxMessageLength)
            {
                message = message.Substring(0, MaxMessageLength) + "... (truncated)";
            }

            _eventLog.WriteEntry(message, entryType.Value);
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
            _eventLog?.Dispose();
        }
    }
}
