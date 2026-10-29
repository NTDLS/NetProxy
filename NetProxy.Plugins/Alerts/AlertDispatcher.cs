using System.Threading.Channels;

namespace NetProxy.Plugins.Alerts
{
    /// <summary>
    /// Sends the alerts from a task of its own, so that a slow webhook never holds up the traffic. The same thing is not told more than
    /// once in <c>minGap</c>; what was held back is counted and mentioned the next time. A recovery is always told.
    /// </summary>
    internal sealed class AlertDispatcher : IDisposable
    {
        private readonly IReadOnlyList<IAlertChannel> _channels;
        private readonly string _source;
        private readonly TimeSpan _minGap;
        private readonly TimeProvider _clock;
        private readonly Action<bool, string> _log;
        private readonly Channel<AlertMessage> _queue = Channel.CreateBounded<AlertMessage>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly Dictionary<string, (DateTime Last, int Held)> _told = [];
        private readonly CancellationTokenSource _stop = new();
        private Task? _worker;

        public AlertDispatcher(IReadOnlyList<IAlertChannel> channels, string source, TimeSpan minGap, TimeProvider clock, Action<bool, string> log)
        {
            _channels = channels;
            _source = source;
            _minGap = minGap;
            _clock = clock;
            _log = log;
        }

        public int ChannelCount => _channels.Count;

        public void Start() => _worker = Task.Run(RunAsync);

        /// <summary>
        /// Queues an alert, unless the same thing was told a moment ago. Returns false when it was held back.
        /// </summary>
        public bool Tell(AlertMessage alert)
        {
            if (_channels.Count == 0)
            {
                return false;
            }

            if (alert.Severity != AlertSeverity.Recovered)
            {
                lock (_told)
                {
                    var now = _clock.GetUtcNow().UtcDateTime;
                    if (_told.TryGetValue(alert.Key, out var last) && now - last.Last < _minGap)
                    {
                        _told[alert.Key] = (last.Last, last.Held + 1);
                        return false;
                    }

                    _told[alert.Key] = (now, 0);

                    if (last.Held > 0)
                    {
                        alert = alert with { Text = alert.Text + $"\n({last.Held} more like this were held back.)" };
                    }
                }
            }

            return _queue.Writer.TryWrite(alert);
        }

        private async Task RunAsync()
        {
            try
            {
                await foreach (var alert in _queue.Reader.ReadAllAsync(_stop.Token))
                {
                    await DeliverAsync(alert);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task DeliverAsync(AlertMessage alert)
        {
            foreach (var channel in _channels)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    await channel.SendAsync(alert, _source, timeout.Token);
                }
                catch (Exception ex) when (_stop.IsCancellationRequested == false)
                {
                    _log(true, $"The alert '{alert.Title}' could not be sent to {channel.Name}: {ex.GetBaseException().Message}");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        /// <summary>
        /// Sends what is queued, waiting a few seconds at most, and stops.
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

            _stop.Cancel();
        }

        public void Dispose() => _stop.Dispose();
    }
}
