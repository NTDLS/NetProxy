namespace NetProxy.Library.Logging
{
    /// <summary>
    /// A destination for log entries (console, file, event log, etc).
    /// Sinks are only ever called from the logger's single background thread.
    /// </summary>
    public interface ILogSink : IDisposable
    {
        public void Write(LogEntry entry);

        /// <summary>
        /// Called after each batch of entries has been written, so that buffered sinks can flush.
        /// </summary>
        public void Flush();
    }
}
