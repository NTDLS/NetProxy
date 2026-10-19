using NetProxy.Library.Logging;
using System.Diagnostics;
using System.Threading.Channels;

namespace NetProxy.Library.Utilities
{
    /// <summary>
    /// The logger. It captures entries on the calling thread and writes them to its sinks (console, rolling file, event log...)
    /// from a single background thread, so callers never wait on file, console or event log I/O.
    /// </summary>
    public class NpLogging : IDisposable
    {
        /// <summary>
        /// From the most detailed to the most important. The level of a logger is the lowest severity that is written.
        /// </summary>
        public enum Severity
        {
            Verbose = 0,
            Information = 1,
            Warning = 2,
            Exception = 3
        }

        public class LoggingPayload
        {
            public Severity Severity { get; set; }
            public Exception? Exception { get; set; }
            public string CustomText { get; set; } = string.Empty;
            public object UserData { get; set; } = string.Empty;
        }

        /// <summary>
        /// When this many entries are waiting to be written the oldest are discarded, so that a stalled sink
        ///     cannot consume unbounded memory. The number discarded is reported once the backlog clears.
        /// </summary>
        private const int MaxQueuedEntries = 50_000;

        private const int MaxEntriesPerBatch = 1_000;

        private readonly Channel<LogEntry> _channel;
        private readonly Thread _writerThread;
        private readonly List<ILogSink> _sinks;
        private long _droppedCount;
        private volatile Severity _level;
        private bool _isDisposed;

        /// <summary>
        /// The lowest severity that is written. Can be changed while the service is running.
        /// </summary>
        public Severity Level
        {
            get => _level;
            set => _level = value;
        }

        public NpLogging(Severity level, params ILogSink[] sinks)
        {
            _level = level;
            _sinks = sinks.ToList();

            _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(MaxQueuedEntries)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            }, (_) => Interlocked.Increment(ref _droppedCount));

            _writerThread = new Thread(WriterThreadProc)
            {
                IsBackground = true,
                Name = "LogWriterThread"
            };
            _writerThread.Start();
        }

        public void Write(LoggingPayload payload)
        {
            Write(payload.Severity, payload.CustomText ?? string.Empty, payload.Exception);
        }

        public void Write(string eventText, Exception exception)
            => Write(Severity.Exception, $"{eventText}: {exception.GetBaseException().Message}", exception);

        public void Write(Severity severity, string eventText)
            => Write(severity, eventText, null);

        public void Write(Severity severity, string text, Exception? exception)
        {
            if (severity < _level)
            {
                return;
            }

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Severity = severity,
                Message = text,
                ExceptionDetail = exception?.ToString(), //Includes the type, stack trace and inner exceptions.
                ThreadId = Environment.CurrentManagedThreadId
            };

            if (_channel.Writer.TryWrite(entry) == false)
            {
                //Only fails once the logger has been disposed.
                Trace.WriteLine(entry.Format());
            }
        }

        private void WriterThreadProc()
        {
            var reader = _channel.Reader;

            while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                int count = 0;
                while (count < MaxEntriesPerBatch && reader.TryRead(out var entry))
                {
                    Dispatch(entry);
                    count++;
                }

                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0)
                {
                    Dispatch(new LogEntry
                    {
                        Timestamp = DateTimeOffset.Now,
                        Severity = Severity.Warning,
                        Message = $"The logger fell behind and discarded {dropped:n0} log entries.",
                        ThreadId = Environment.CurrentManagedThreadId
                    });
                }

                foreach (var sink in _sinks)
                {
                    try
                    {
                        sink.Flush();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"NetProxy: Log sink {sink.GetType().Name} failed to flush: {ex}");
                    }
                }
            }
        }

        private void Dispatch(LogEntry entry)
        {
            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Write(entry);
                }
                catch (Exception ex)
                {
                    //One broken sink must not stop the others.
                    Trace.TraceError($"NetProxy: Log sink {sink.GetType().Name} failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Stops accepting entries and waits (briefly) for the queue to be written out.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            _channel.Writer.TryComplete();
            _writerThread.Join(TimeSpan.FromSeconds(5));

            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Dispose();
                }
                catch
                {
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
