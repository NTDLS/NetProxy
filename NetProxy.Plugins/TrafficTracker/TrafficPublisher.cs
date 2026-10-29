using NetProxy.Plugins.Common.TrafficTracker;
using NTDLS.CatMQ.Client;
using System.Threading.Channels;

namespace NetProxy.Plugins.TrafficTracker
{
    /// <summary>
    /// Takes the requests that the plug-in saw and sends them to the CatMQ queue in batches, in the background and without waiting for the
    /// server to confirm (fire and forget). Nothing in here is allowed to hold up, or to fail, the traffic that is being tracked: the plug-in
    /// only puts a record in a bounded buffer, which drops its oldest records when the server cannot be reached for long enough to fill it.
    /// </summary>
    public sealed class TrafficPublisher : IDisposable
    {
        private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan _logInterval = TimeSpan.FromMinutes(1);

        private readonly TrackerSettings _settings;
        private readonly Action<string, bool> _log;
        private readonly Channel<WebRequestRecord> _buffer;
        private readonly CMqClient _client = new();
        private readonly CancellationTokenSource _stop = new();
        private Task? _connectTask;
        private Task? _sendTask;
        private long _enqueued;
        private long _sent;
        private long _dropped;
        private DateTime _lastLoggedUtc = DateTime.UtcNow;
        private int _queueCreated;

        /// <param name="settings">What the plug-in is set up with.</param>
        /// <param name="log">Where messages go: the text, and whether it is a warning.</param>
        public TrafficPublisher(TrackerSettings settings, Action<string, bool> log)
        {
            _settings = settings;
            _log = log;

            _buffer = Channel.CreateBounded<WebRequestRecord>(new BoundedChannelOptions(settings.MaxQueued)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            }, _ => Interlocked.Increment(ref _dropped));

            _client.OnException += (_, _, ex) => _log($"The message queue reported an error: {ex.GetBaseException().Message}", true);
            _client.OnConnected += (_, isReconnect) => OnConnected(isReconnect);
            _client.OnDisconnected += _ => _log("The connection to the message queue was lost. Requests are kept and sent when it comes back.", true);
        }

        public bool IsConnected => _client.IsConnected;

        /// <summary>How many requests were sent to the queue.</summary>
        public long Sent => Interlocked.Read(ref _sent);

        /// <summary>How many requests were dropped because the buffer was full.</summary>
        public long Dropped => Interlocked.Read(ref _dropped);

        public void Start()
        {
            _connectTask = Task.Run(ConnectLoopAsync);
            _sendTask = Task.Run(SendLoopAsync);
        }

        /// <summary>
        /// Keeps a request to be sent. Never blocks and never throws.
        /// </summary>
        public void Enqueue(WebRequestRecord record)
        {
            if (_buffer.Writer.TryWrite(record))
            {
                Interlocked.Increment(ref _enqueued);
            }
        }

        private async Task ConnectLoopAsync()
        {
            var warned = false;

            while (_stop.IsCancellationRequested == false)
            {
                if (_client.IsConnected == false)
                {
                    try
                    {
                        _client.Connect(_settings.MqHost, _settings.MqPort);
                        warned = false;
                    }
                    catch (Exception ex)
                    {
                        if (warned == false)
                        {
                            _log($"Could not connect to the message queue at {_settings.MqHost}:{_settings.MqPort} ({ex.GetBaseException().Message}). Trying again every {_reconnectDelay.TotalSeconds:0} seconds; requests are kept meanwhile.", true);
                            warned = true;
                        }
                    }
                }

                try
                {
                    await Task.Delay(_reconnectDelay, _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void OnConnected(bool isReconnect)
        {
            _log(isReconnect ? "Reconnected to the message queue." : $"Connected to the message queue at {_settings.MqHost}:{_settings.MqPort}.", false);

            //The ingestion service makes the queue too. Whichever of them gets there first does it, and making one that exists does nothing.
            Task.Run(() =>
            {
                try
                {
                    _client.CreateQueue(TrafficQueue.CreateConfiguration(_settings.Queue));
                    Interlocked.Exchange(ref _queueCreated, 1);
                }
                catch (Exception ex)
                {
                    _log($"The queue '{_settings.Queue}' could not be made: {ex.GetBaseException().Message}", true);
                }
            });
        }

        private async Task SendLoopAsync()
        {
            var batch = new List<WebRequestRecord>(_settings.BatchSize);

            try
            {
                while (_stop.IsCancellationRequested == false)
                {
                    //Records stay in the buffer while the server cannot be reached, so that they are not lost for the sake of a short outage.
                    if (_client.IsConnected == false || Volatile.Read(ref _queueCreated) == 0)
                    {
                        await Task.Delay(500, _stop.Token);
                        LogProgress();
                        continue;
                    }

                    batch.Clear();

                    //The first record, then whatever comes within the interval, up to the size of a batch.
                    using var interval = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    try
                    {
                        if (await _buffer.Reader.WaitToReadAsync(_stop.Token) == false)
                        {
                            return;
                        }

                        interval.CancelAfter(_settings.FlushIntervalMs);
                        while (batch.Count < _settings.BatchSize)
                        {
                            if (_buffer.Reader.TryRead(out var record))
                            {
                                batch.Add(record);
                            }
                            else if (await _buffer.Reader.WaitToReadAsync(interval.Token) == false)
                            {
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_stop.IsCancellationRequested == false)
                    {
                        //The interval is over: what there is gets sent.
                    }

                    await SendAsync(batch);
                    LogProgress();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log($"Sending to the message queue stopped: {ex.GetBaseException().Message}", true);
            }
        }

        private async Task SendAsync(List<WebRequestRecord> records)
        {
            if (records.Count == 0)
            {
                return;
            }

            try
            {
                await _client.EnqueueExpedientAsync(_settings.Queue, new WebRequestBatch
                {
                    Source = _settings.Source,
                    Requests = [.. records]
                });

                Interlocked.Add(ref _sent, records.Count);
            }
            catch (Exception ex)
            {
                //Fire and forget: a batch that could not be sent is not sent again, so that a server that cannot keep up does not make the
                //  plug-in pile up work.
                Interlocked.Add(ref _dropped, records.Count);
                _log($"A batch of {records.Count} requests could not be sent: {ex.GetBaseException().Message}", true);
            }
        }

        private void LogProgress()
        {
            if (DateTime.UtcNow - _lastLoggedUtc < _logInterval)
            {
                return;
            }

            _lastLoggedUtc = DateTime.UtcNow;
            var dropped = Dropped;
            if (dropped > 0)
            {
                _log($"{Sent:n0} requests sent so far, {dropped:n0} dropped because the message queue could not take them.", true);
            }
        }

        /// <summary>
        /// Stops, after a last attempt to send what is waiting (when the server can be reached).
        /// </summary>
        public void Stop(TimeSpan flushTime)
        {
            try
            {
                var until = DateTime.UtcNow + flushTime;

                //Stop reading new work, then send what is left.
                _stop.Cancel();
                try { Task.WaitAll([_connectTask ?? Task.CompletedTask, _sendTask ?? Task.CompletedTask], TimeSpan.FromSeconds(2)); } catch { }

                if (_client.IsConnected && Volatile.Read(ref _queueCreated) == 1)
                {
                    var rest = new List<WebRequestRecord>();
                    while (DateTime.UtcNow < until && _buffer.Reader.TryRead(out var record))
                    {
                        rest.Add(record);

                        if (rest.Count >= _settings.BatchSize)
                        {
                            SendAsync(rest).GetAwaiter().GetResult();
                            rest = [];
                        }
                    }

                    SendAsync(rest).GetAwaiter().GetResult();
                }
            }
            catch
            {
                //Stopping is not a place to fail.
            }
            finally
            {
                try { _client.Disconnect(); } catch { }
            }
        }

        public void Dispose()
        {
            Stop(TimeSpan.FromSeconds(1));
            _client.Dispose();
            _stop.Dispose();
        }
    }
}
