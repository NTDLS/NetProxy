using NTDLS.CatMQ.Shared;

namespace NetProxy.TrafficTracker.Contracts
{
    /// <summary>
    /// The queue that the plug-in and the ingestion service meet on, and how it is made. Both create it, so it does not matter which of
    /// them starts first: creating a queue that exists does nothing.
    /// </summary>
    public static class TrafficQueue
    {
        public const string DefaultName = "NetProxy.Traffic";
        public const int DefaultPort = 45784;

        /// <summary>
        /// A queue that keeps its messages on disk, so that nothing is lost while the ingestion service or the database is down, and
        /// that gives up on messages (to a dead-letter queue, named like this one with .dlq) that cannot be ingested.
        /// </summary>
        public static CMqQueueConfiguration CreateConfiguration(string queueName) => new(queueName)
        {
            PersistenceScheme = CMqPersistenceScheme.Persistent,

            //More than one ingestion service can read the queue, and a batch only has to be ingested by one of them.
            ConsumptionScheme = CMqConsumptionScheme.FirstConsumedSubscriber,
            DeliveryScheme = CMqDeliveryScheme.Balanced,
            MaxDeliveryAttempts = 20,
            MaxMessageAge = TimeSpan.FromDays(7),
            DeadLetterConfiguration = new CMqDeadLetterQueueConfiguration
            {
                PersistenceScheme = CMqPersistenceScheme.Persistent,
                MaxMessageAge = TimeSpan.FromDays(30)
            }
        };
    }
}
