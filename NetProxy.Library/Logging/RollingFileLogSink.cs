using System.Diagnostics;
using System.Text;

namespace NetProxy.Library.Logging
{
    /// <summary>
    /// Appends log entries to a file, rolling it over once it reaches a size limit:
    ///     NetProxy.log -> NetProxy.1.log -> NetProxy.2.log ... up to the retained file count.
    /// </summary>
    public class RollingFileLogSink : ILogSink
    {
        private readonly object _lock = new();
        private StreamWriter? _writer;
        private string _path = string.Empty;
        private long _maxFileBytes;
        private int _retainedFileCount;
        private bool _hasFailed;

        public RollingFileLogSink(string path, long maxFileBytes, int retainedFileCount)
        {
            Configure(path, maxFileBytes, retainedFileCount);
        }

        /// <summary>
        /// Changes the log file and/or rolling limits. Safe to call while logging is in progress.
        /// An empty path disables file logging.
        /// </summary>
        public void Configure(string path, long maxFileBytes, int retainedFileCount)
        {
            lock (_lock)
            {
                //Services run with System32 as the working directory, so anchor relative paths to the application instead.
                var fullPath = string.IsNullOrWhiteSpace(path) ? string.Empty
                    : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));

                if (fullPath.Equals(_path, StringComparison.OrdinalIgnoreCase) == false)
                {
                    CloseWriter();
                    _path = fullPath;
                    _hasFailed = false;
                }

                _maxFileBytes = Math.Max(1024 * 1024, maxFileBytes);
                _retainedFileCount = Math.Max(0, retainedFileCount);
            }
        }

        public void Write(LogEntry entry)
        {
            lock (_lock)
            {
                if (_path.Length == 0 || _hasFailed)
                {
                    return;
                }

                try
                {
                    _writer ??= OpenWriter();
                    _writer.WriteLine(entry.Format());

                    if (_writer.BaseStream.Length >= _maxFileBytes)
                    {
                        Roll();
                    }
                }
                catch (Exception ex)
                {
                    //We can't log a logging failure to the log; report it once and stop trying until the path changes.
                    _hasFailed = true;
                    CloseWriter();
                    Trace.TraceError($"NetProxy: Failed to write log file '{_path}': {ex}");
                    Console.Error.WriteLine($"Failed to write log file '{_path}': {ex.Message}");
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                }
                catch
                {
                }
            }
        }

        private StreamWriter OpenWriter()
        {
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }

            //Append (rather than overwrite) so that a service restart does not destroy the log that explains why it restarted.
            var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return new StreamWriter(stream, new UTF8Encoding(false));
        }

        private void Roll()
        {
            CloseWriter();

            if (_retainedFileCount == 0)
            {
                File.Delete(_path);
                return;
            }

            var directory = Path.GetDirectoryName(_path) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(_path);
            var extension = Path.GetExtension(_path);
            string RolledPath(int index) => Path.Combine(directory, $"{baseName}.{index}{extension}");

            //Shift NetProxy.(n-1).log -> NetProxy.n.log, discarding the oldest.
            File.Delete(RolledPath(_retainedFileCount));
            for (int index = _retainedFileCount - 1; index >= 1; index--)
            {
                if (File.Exists(RolledPath(index)))
                {
                    File.Move(RolledPath(index), RolledPath(index + 1));
                }
            }
            File.Move(_path, RolledPath(1));
        }

        private void CloseWriter()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
            }
            _writer = null;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                CloseWriter();
            }
        }
    }
}
