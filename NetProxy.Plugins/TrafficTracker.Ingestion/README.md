# NetProxy Traffic Tracker (ingestion service)

The service that reads the web requests that the [Traffic Tracker plug-in](../TrafficTracker.Plugin) sends to a [CatMQ](https://github.com/NTDLS/CatMQ) queue and writes them to a SQL Server database. It is a Windows service or a systemd service when it is run as one (the service name is `NetProxyTrafficIngestion`), and an ordinary console program when it is not.

Both the plug-in and this service are clients of a [CatMQ](https://github.com/NTDLS/CatMQ) server (`NTDLS.CatMQ.Server`, default port `45784`), which has to be running where both can reach it; `Mq:Host` is that machine. The queue is made by whichever of the two starts first, and keeps its messages on disk, so nothing is lost while this service or the database is down; a message that can not be ingested after 20 tries goes to a dead-letter queue (`NetProxy.Traffic.dlq`). The service keeps trying to connect until it can.

## Settings

In `appsettings.json` next to the program, or as environment variables with `__` for the levels (`Mq__Host`).

| setting | default | |
|---|---|---|
| `ConnectionStrings:Traffic` | | The database that the traffic is written to. |
| `Mq:Host`, `Mq:Port` | `localhost`, `45784` | Where the queue is. |
| `Mq:Queue` | `NetProxy.Traffic` | The queue. The plug-in has to use the same. |
| `Ingestion:CreateDatabaseIfMissing` | `false` | Make the database when it is not there. The login needs the right to. |
| `Ingestion:RetentionDays` | `0` | Requests older than this are removed. `0` keeps them for ever. |
| `Ingestion:PurgeBatchSize` | `10000` | How many requests the removal deletes at a time. |
| `Serilog` | console, and `Logs/ingestion-.log` | A file a day, 14 kept. |

The schema is made and brought up to date by the service when it starts (the scripts are in `Database/Scripts`).

## Querying

The names of hosts, paths, clients, user agents and so on are kept once and the requests point at them, so read the view:

```sql
SELECT TOP 100 * FROM traffic.vWebRequest ORDER BY ObservedUtc DESC;
```

`traffic.vWebRequest` has `RequestId, ObservedUtc, Source, Proxy, ListenPort, IsTls, Host, ClientAddress, Method, Path, QueryString, HttpVersion, StatusCode, ContentType, RequestBytes, ResponseBytes, DurationMs, UserAgent, Browser, OperatingSystem, DeviceType, IsBot, Referrer, ReferrerHost and ConnectionId`. While the service is writing, a read can be chosen as a deadlock victim: read with `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` for statistics.
