using NetProxy.Library.Utilities;

namespace NetProxy.Library.Logging
{
    /// <summary>
    /// A single log event, captured on the calling thread and written to the sinks by the logger's background thread.
    /// </summary>
    public class LogEntry
    {
        public DateTimeOffset Timestamp { get; init; }
        public NpLogging.Severity Severity { get; init; }
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Full exception detail (type, message, stack trace and inner exceptions), if the entry was logged with an exception.
        /// </summary>
        public string? ExceptionDetail { get; init; }

        public int ThreadId { get; init; }

        /// <summary>
        /// e.g. "2026-10-18 14:03:27.512 -05:00 [Warning] (T:12) message"
        /// </summary>
        public string Format(bool includeExceptionDetail = true)
        {
            var text = $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Severity}] (T:{ThreadId}) {Message}";

            if (includeExceptionDetail && string.IsNullOrEmpty(ExceptionDetail) == false)
            {
                text += Environment.NewLine + ExceptionDetail;
            }

            return text;
        }
    }
}
