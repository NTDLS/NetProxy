using NetProxy.Library.Payloads;

namespace NetProxy.Client.Classes
{
    /// <summary>
    /// The throughput and connections at one moment.
    /// </summary>
    public readonly record struct PerformanceSample(DateTime Time, double SentPerSecond, double ReceivedPerSecond, int Connections);

    /// <summary>
    /// Turns the running totals that the service reports into the rates the performance chart shows, for all of the proxies together
    /// and for each proxy. The rates are worked out from the time that actually passed between two samples, so they are right
    /// whether the samples are one second apart or ten.
    /// </summary>
    public class PerformanceHistory
    {
        /// <summary>
        /// The key of the history of all of the proxies together.
        /// </summary>
        public static readonly Guid All = Guid.Empty;

        /// <summary>
        /// How long the chart goes back.
        /// </summary>
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

        private class Series
        {
            public readonly List<PerformanceSample> Samples = new();
            public ulong LastBytesSent;
            public ulong LastBytesReceived;
            public DateTime? LastTime;
        }

        private readonly Dictionary<Guid, Series> _series = new();

        /// <summary>
        /// Adds the statistics that the service reported at <paramref name="time"/>.
        /// </summary>
        public void Add(IReadOnlyList<NpProxyGridStats> statistics, DateTime time)
        {
            AddSample(All, time,
                statistics.Aggregate(0UL, (sum, o) => sum + o.BytesWritten),
                statistics.Aggregate(0UL, (sum, o) => sum + o.BytesRead),
                statistics.Sum(o => o.CurrentConnections));

            foreach (var proxy in statistics)
            {
                AddSample(proxy.Id, time, proxy.BytesWritten, proxy.BytesRead, proxy.CurrentConnections);
            }

            //Proxies that were deleted no longer need their history.
            var ids = statistics.Select(o => o.Id).ToHashSet();
            foreach (var key in _series.Keys.Where(o => o != All && ids.Contains(o) == false).ToList())
            {
                _series.Remove(key);
            }
        }

        private void AddSample(Guid key, DateTime time, ulong bytesSent, ulong bytesReceived, int connections)
        {
            if (_series.TryGetValue(key, out var series) == false)
            {
                series = new Series();
                _series.Add(key, series);
            }

            double sent = 0;
            double received = 0;

            if (series.LastTime is DateTime lastTime && time > lastTime)
            {
                var seconds = (time - lastTime).TotalSeconds;

                //The totals start over when the service restarts (or a proxy is saved), which is not a negative rate.
                sent = bytesSent >= series.LastBytesSent ? (bytesSent - series.LastBytesSent) / seconds : 0;
                received = bytesReceived >= series.LastBytesReceived ? (bytesReceived - series.LastBytesReceived) / seconds : 0;

                series.Samples.Add(new PerformanceSample(time, sent, received, connections));
            }
            else if (series.LastTime == null)
            {
                //There is nothing to compare the first totals with, but the connections are known.
                series.Samples.Add(new PerformanceSample(time, 0, 0, connections));
            }

            series.LastBytesSent = bytesSent;
            series.LastBytesReceived = bytesReceived;
            series.LastTime = time;

            var oldest = time - Window - TimeSpan.FromSeconds(5);
            series.Samples.RemoveAll(o => o.Time < oldest);
        }

        public IReadOnlyList<PerformanceSample> Get(Guid key)
            => _series.TryGetValue(key, out var series) ? series.Samples : [];

        /// <summary>
        /// Forgets everything, for example when the console connects to another service.
        /// </summary>
        public void Clear() => _series.Clear();
    }
}
