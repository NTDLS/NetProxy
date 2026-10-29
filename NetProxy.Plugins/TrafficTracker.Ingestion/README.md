# NetProxy Traffic Tracker (ingestion service)

The service that reads the web requests that the [Traffic Tracker plug-in](../TrafficTracker) sends to a [CatMQ](https://github.com/NTDLS/CatMQ) queue and writes them to a SQL Server database. It is a Windows service or a systemd service when it is run as one (the service name is `NetProxyTrafficIngestion`), and an ordinary console program when it is not.

Both the plug-in and this service are clients of a [CatMQ](https://github.com/NTDLS/CatMQ) server (`NTDLS.CatMQ.Server`, default port `45784`), which has to be running where both can reach it; `Mq:Host` is that machine. The queue is made by whichever of the two starts first, and keeps its messages on disk, so nothing is lost while this service or the database is down; a message that can not be ingested after 20 tries goes to a dead-letter queue (`NetProxy.Traffic.dlq`). The service keeps trying to connect until it can.

## Where to find it

It comes with every NetProxy package, in the `Plugins` folder with the plugins, in a folder of its own (`Plugins\TrafficTracker.Ingestion`). It is not a plugin (the NetProxy service does not load it): it is what the traffic tracker plugin needs on the other end. **It is not started and not installed as a service**: it has to be told which database and which queue first (see Settings).

* **Windows installer:** part of the *Ready-made Plugins* component, in `Plugins\TrafficTracker.Ingestion` in the install folder. An upgrade keeps your `appsettings.json`.
* **Linux zips:** `Plugins/TrafficTracker.Ingestion/`, self contained like the service (nothing to install). `chmod +x NetProxy.Plugins.TrafficTracker.Ingestion` after unzipping.
* **`Publish.bat`** in the folder above makes `NetProxy.Plugins.TrafficTracker.Ingestion.zip`, a portable build that needs the .NET 10 runtime.

## Run it

Edit `appsettings.json` first, then run the program by hand once to see that it connects (it logs to the console and to `Logs`). Then run it as a service:

**Windows** (an elevated prompt; the account that runs the service needs the right to read and write the database named in the connection string, which is the machine account or a service account when it says `Integrated Security=True`):

```
sc.exe create NetProxyTrafficIngestion binPath= "C:\Program Files\NetworkDLS\NetProxy\Plugins\TrafficTracker.Ingestion\NetProxy.Plugins.TrafficTracker.Ingestion.exe" start= delayed-auto
sc.exe start NetProxyTrafficIngestion
```

**Linux** (systemd), in `/etc/systemd/system/netproxy-ingestion.service`:

```ini
[Unit]
Description=NetProxy traffic ingestion
After=network.target

[Service]
WorkingDirectory=/home/<user>/apps/NetProxy/Plugins/TrafficTracker.Ingestion
ExecStart=/home/<user>/apps/NetProxy/Plugins/TrafficTracker.Ingestion/NetProxy.Plugins.TrafficTracker.Ingestion
User=<user>
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

then `sudo systemctl daemon-reload && sudo systemctl enable --now netproxy-ingestion`.

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
