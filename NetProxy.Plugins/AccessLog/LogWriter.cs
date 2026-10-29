using System.Text;
using System.Threading.Channels;

namespace NetProxy.Plugins.AccessLog
{
    /// <summary>
    /// Appends lines to a log file from a task of its own, so that writing never holds up the traffic. A new file is started every day
    /// (<c>access-2026-10-28.log</c>) and, when MaxFileBytes is set, when the file gets too big (<c>access-2026-10-28.1.log</c>). Files
    /// older than KeepDays are deleted. If lines come faster than the disk takes them, the oldest are dropped and counted.
    /// </summary>
    internal sealed class LogWriter : IDisposable
    {
        private readonly string _directory;
        private readonly string _pattern;
        private readonly long _maxFileBytes;
        private readonly int _keepDays;
        private readonly TimeProvider _clock;
        private readonly Action<string> _warn;
        private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(50_000) { FullMode = BoundedChannelFullMode.DropOldest });
        private Task? _worker;
        private StreamWriter? _writer;
        private string? _currentPath;
        private DateTime _currentDay;
        private int _index;
        private DateTime _lastCleanup;
        private long _dropped;

        public LogWriter(string directory, string pattern, long maxFileBytes, int keepDays, TimeProvider clock, Action<string> warn)
        {
            _directory = directory;
            _pattern = pattern;
            _maxFileBytes = maxFileBytes;
            _keepDays = keepDays;
            _clock = clock;
            _warn = warn;
        }

        public void Start()
        {
            Directory.CreateDirectory(_directory);
            _worker = Task.Run(RunAsync);
        }

        public void Write(string line) => _queue.Writer.TryWrite(line);

        /// <summary>The path of the file for a day and an index.</summary>
        internal string PathFor(DateTime day, int index)
        {
            var name = _pattern.Replace("{date}", day.ToString("yyyy-MM-dd"));
            if (index > 0)
            {
                name = Path.GetFileNameWithoutExtension(name) + "." + index + Path.GetExtension(name);
            }

            return Path.Combine(_directory, name);
        }

        private async Task RunAsync()
        {
            var reader = _queue.Reader;

            try
            {
                while (await reader.WaitToReadAsync())
                {
                    while (reader.TryRead(out var line))
                    {
                        Append(line);
                    }

                    _writer?.Flush();
                }
            }
            catch (Exception ex)
            {
                _warn($"Writing the access log stopped: {ex.GetBaseException().Message}");
            }
        }

        private void Append(string line)
        {
            try
            {
                var day = _clock.GetUtcNow().UtcDateTime.Date;

                if (_writer == null || day != _currentDay || (_maxFileBytes > 0 && _writer.BaseStream.Length >= _maxFileBytes))
                {
                    Open(day);
                }

                _writer!.WriteLine(line);
            }
            catch (Exception ex)
            {
                //The disk is full or the file is locked: this line is lost, the next one tries again.
                if (Interlocked.Increment(ref _dropped) == 1)
                {
                    _warn($"A line could not be written to the access log: {ex.GetBaseException().Message}");
                }

                _writer?.Dispose();
                _writer = null;
            }
        }

        private void Open(DateTime day)
        {
            _writer?.Dispose();

            _index = day == _currentDay && _writer != null ? _index + 1 : 0;
            _currentDay = day;

            //A restart in the middle of the day carries on with the file that was there, or the next if that one is full.
            var path = PathFor(day, _index);
            while (_maxFileBytes > 0 && File.Exists(path) && new FileInfo(path).Length >= _maxFileBytes)
            {
                path = PathFor(day, ++_index);
            }

            _currentPath = path;
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));

            Cleanup(day);
        }

        private void Cleanup(DateTime day)
        {
            if (_keepDays <= 0 || day == _lastCleanup)
            {
                return;
            }

            _lastCleanup = day;

            try
            {
                var wildcard = _pattern.Replace("{date}", "*");
                var oldest = day.AddDays(-_keepDays);

                foreach (var file in Directory.EnumerateFiles(_directory, Path.GetFileNameWithoutExtension(wildcard) + "*" + Path.GetExtension(wildcard)))
                {
                    if (File.GetLastWriteTimeUtc(file) < oldest && file != _currentPath)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _warn($"Old access logs could not be removed: {ex.GetBaseException().Message}");
            }
        }

        /// <summary>
        /// Writes what is queued, waiting a few seconds at most, and closes the file.
        /// </summary>
        public void Stop(TimeSpan wait)
        {
            _queue.Writer.TryComplete();

            try
            {
                _worker?.Wait(wait);
            }
            catch
            {
            }

            _writer?.Dispose();
            _writer = null;
        }

        public void Dispose() => _writer?.Dispose();
    }
}
