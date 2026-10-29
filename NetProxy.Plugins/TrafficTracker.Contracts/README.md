# NetProxy Traffic Tracker (contracts)

What the [Traffic Tracker plug-in](../TrafficTracker.Plugin) and the [ingestion service](../TrafficTracker.Ingestion) agree on: the messages that carry the web requests (`WebRequestMessages.cs`), and the queue they travel on (`TrafficQueue.cs`: its default name `NetProxy.Traffic`, its default port `45784`, and how it is made: persistent, with a dead-letter queue, so that nothing is lost while the service or the database is down).

Both sides create the queue, so it does not matter which of them starts first.
