using NetProxy.Library;
using NTDLS.Persistence;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// What was counted for one endpoint of a proxy, as it is written to the statistics file.
    /// </summary>
    public class NpEndpointStatisticsRecord
    {
        public Guid EndpointId { get; set; }
        public ulong TotalConnections { get; set; }
        public ulong BytesRead { get; set; }
        public ulong BytesWritten { get; set; }
    }

    /// <summary>
    /// What was counted for one proxy (and its endpoints), as it is written to the statistics file. The connections that are open
    /// are not counted here: they end with the service.
    /// </summary>
    public class NpProxyStatisticsRecord
    {
        public Guid ProxyId { get; set; }
        public ulong TotalConnections { get; set; }
        public ulong BytesRead { get; set; }
        public ulong BytesWritten { get; set; }
        public List<NpEndpointStatisticsRecord> Endpoints { get; set; } = new();

        /// <summary>
        /// When the numbers were taken from the proxy.
        /// </summary>
        public DateTime CapturedUtc { get; set; }
    }

    /// <summary>
    /// Keeps the statistics of the proxies across restarts of the service, and across the changes that replace a proxy (editing it).
    /// The numbers are written every few minutes and when the service stops, so what a crash loses is the last few minutes, which is
    /// fine for counters that are there to be looked at.
    /// </summary>
    internal sealed class NpStatisticsStore : IDisposable
    {
        /// <summary>
        /// A record of a proxy that no longer exists is kept this long, because a proxy that is edited is replaced by a new one that takes
        /// over its numbers a moment later.
        /// </summary>
        private static readonly TimeSpan _keepGone = TimeSpan.FromMinutes(5);

        private readonly object _lock = new();
        private readonly object _saveLock = new();
        private Dictionary<Guid, NpProxyStatisticsRecord> _records = [];
        private Func<IEnumerable<NpProxy>> _proxies = () => [];
        private Timer? _timer;
        private string _lastWritten = string.Empty;

        /// <summary>
        /// Reads the statistics file. This is before the proxies are made, so that they can take their numbers from it.
        /// </summary>
        public void Load()
        {
            try
            {
                var records = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, new List<NpProxyStatisticsRecord>());

                lock (_lock)
                {
                    _records = records.GroupBy(o => o.ProxyId).ToDictionary(o => o.Key, o => o.First());
                }
            }
            catch (Exception ex)
            {
                //A file that cannot be read means starting from zero, which is no reason not to start.
                Singletons.Logging.Write("The statistics could not be read, so they start from zero", ex);
                lock (_lock)
                {
                    _records = [];
                }
            }
        }

        /// <summary>
        /// Starts writing the statistics now and then.
        /// </summary>
        public void Start(Func<IEnumerable<NpProxy>> proxies)
        {
            _proxies = proxies;
            _timer?.Dispose();

            var interval = TimeSpan.FromSeconds(Constants.StatisticsSaveIntervalSeconds);
            _timer = new Timer(_ => Save(), null, interval, interval);
        }

        /// <summary>
        /// Gives a new proxy the numbers that the proxy with its id had.
        /// </summary>
        public void Restore(NpProxy proxy)
        {
            NpProxyStatisticsRecord? record;
            lock (_lock)
            {
                _records.TryGetValue(proxy.Configuration.Id, out record);
            }

            if (record == null)
            {
                return;
            }

            proxy.Statistics.Restore(record.TotalConnections, record.BytesRead, record.BytesWritten);

            var endpointIds = proxy.Configuration.Endpoints.Collection.Select(o => o.Id).ToHashSet();
            foreach (var endpoint in record.Endpoints.Where(o => endpointIds.Contains(o.EndpointId)))
            {
                var statistics = proxy.EndpointStatistics.GetOrAdd(endpoint.EndpointId, _ => new NpProxyEndpointStatistics());
                statistics.Restore(endpoint.TotalConnections, endpoint.BytesRead, endpoint.BytesWritten);
            }
        }

        /// <summary>
        /// Takes the numbers of the proxy, so that they are written the next time, and so that a proxy that replaces it gets them.
        /// </summary>
        public void Capture(NpProxy proxy)
        {
            var record = new NpProxyStatisticsRecord
            {
                ProxyId = proxy.Configuration.Id,
                TotalConnections = proxy.Statistics.TotalConnections,
                BytesRead = proxy.Statistics.BytesRead,
                BytesWritten = proxy.Statistics.BytesWritten,
                CapturedUtc = DateTime.UtcNow,
                Endpoints = proxy.EndpointStatistics.Select(o => new NpEndpointStatisticsRecord
                {
                    EndpointId = o.Key,
                    TotalConnections = o.Value.TotalConnections,
                    BytesRead = o.Value.BytesRead,
                    BytesWritten = o.Value.BytesWritten
                }).ToList()
            };

            lock (_lock)
            {
                _records[record.ProxyId] = record;
            }
        }

        /// <summary>
        /// Takes the numbers of the proxies that exist and writes them. Proxies that are gone are forgotten after a while.
        /// </summary>
        public void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    var live = _proxies().ToList();
                    foreach (var proxy in live)
                    {
                        Capture(proxy);
                    }

                    List<NpProxyStatisticsRecord> records;
                    lock (_lock)
                    {
                        var liveIds = live.Select(o => o.Configuration.Id).ToHashSet();
                        foreach (var gone in _records.Values.Where(o => liveIds.Contains(o.ProxyId) == false && DateTime.UtcNow - o.CapturedUtc > _keepGone).ToList())
                        {
                            _records.Remove(gone.ProxyId);
                        }

                        records = _records.Values.OrderBy(o => o.ProxyId).ToList();
                    }

                    //Nothing has happened since the last time: the file is not written again for nothing. The time of the capture is not
                    //  part of what is compared.
                    var comparable = System.Text.Json.JsonSerializer.Serialize(records.Select(o => new { o.ProxyId, o.TotalConnections, o.BytesRead, o.BytesWritten, o.Endpoints }));
                    if (comparable == _lastWritten)
                    {
                        return;
                    }

                    CommonApplicationData.SaveToDisk(Singletons.ConfigurationName, records);
                    _lastWritten = comparable;
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to save the statistics", ex);
                }
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;

            Save();
        }
    }
}
