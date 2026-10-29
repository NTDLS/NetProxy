using NetProxy.Plugins.Common.TrafficTracker;

namespace NetProxy.Plugins.TrafficTracker.Ingestion
{
    /// <summary>
    /// What the ingestion service is set up with (appsettings.json, or environment variables such as Mq__Host).
    /// </summary>
    public sealed class IngestionOptions
    {
        /// <summary>The database that the traffic is written to. It is ConnectionStrings:Traffic.</summary>
        public string ConnectionString { get; set; } = string.Empty;

        public string MqHost { get; set; } = "localhost";
        public int MqPort { get; set; } = TrafficQueue.DefaultPort;
        public string Queue { get; set; } = TrafficQueue.DefaultName;

        /// <summary>Make the database when it is not there. The login needs the right to, and it is off unless it is asked for.</summary>
        public bool CreateDatabaseIfMissing { get; set; }

        /// <summary>Requests older than this many days are removed. 0 keeps them forever.</summary>
        public int RetentionDays { get; set; }

        /// <summary>How many requests the removal deletes at a time.</summary>
        public int PurgeBatchSize { get; set; } = 10_000;
    }
}
