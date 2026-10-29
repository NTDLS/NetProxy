# NetProxy Traffic Tracker (plug-in)

Tracks the web traffic that passes through a proxy: for every HTTP request the host, the client, the user agent, the path and more, with the status of the response. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `TrafficTracker.Plugin`; it builds to `NetProxy.Plugins.TrafficTracker.dll`.

The plug-in only looks. It sends what it sees to a queue on a [CatMQ](https://github.com/NTDLS/CatMQ) server, and the [ingestion service](../TrafficTracker.Ingestion) reads the queue and puts it in a SQL Server database, where `traffic.vWebRequest` is the view to query. Nothing here waits for the queue: the requests are put in a buffer that a background task sends from, so the traffic is never held up for the sake of statistics.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.TrafficTracker.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: the plug-in brings the CatMQ client and the contracts with it.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.TrafficTracker.dll`, leave the type name empty, and put the settings in *Settings*, one `name=value` per line.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for.
4. Run a CatMQ server where both can reach it, and install and start the [ingestion service](../TrafficTracker.Ingestion) with the database. The queue is made by whichever of the plug-in and the service starts first.

## Settings

Every setting has a default, so a plug-in with no settings at all sends what it sees to a CatMQ server on this machine.

| setting | default | |
|---|---|---|
| `Mq.Host`, `Mq.Port` | `localhost`, `45784` | The CatMQ server, the same one that the ingestion service uses. |
| `Mq.Queue` | `NetProxy.Traffic` | The queue. |
| `Source` | the machine name | Which NetProxy this is, for when several send to one queue. |
| `BatchSize` | `200` | How many requests go in one message. |
| `FlushIntervalMs` | `1000` | How long a request waits for the others before it is sent. |
| `MaxQueued` | `50000` | How many requests are kept while the queue can not be reached. The oldest go first: the traffic must never be held up, or its memory used up, for statistics. |
| `RecordResponses` | `true` | Keep the response (status, size, how long it took) with the request. |
| `RecordQueryStrings` | `false` | Keep the query string. It can hold things that people would not want kept. |
| `RecordReferrers` | `true` | Keep the `Referer` header (without its query string unless `RecordQueryStrings` is on). |
| `TrustForwardedFor` | `false` | Use the first address of `X-Forwarded-For` as the client, for a NetProxy that is behind another proxy. Only turn it on when that is so: anyone can send the header. |
| `IncludeTcpProxies` | `false` | Also follow TCP proxies, for the ones that carry plain HTTP. |
| `IgnoreHosts` | | Requests for these hosts are not kept: a comma separated list, `*.example.com` for everything below a domain. |
| `IgnorePathPrefixes` | | Paths that start with these are not kept (`/health`). |
| `IgnoreClients` | | Requests from these client addresses are not kept. |

## Good to know

* **HTTP/1.x only.** A request is followed for as long as it is HTTP/1.x; HTTP/2 is compressed and is not followed. Untick *Allow HTTP/2* on the proxy to have those requests tracked. The log says so, once, when the plug-in meets an HTTP/2 client.
* A setting that is wrong is logged and the default is used: the plug-in still loads.
* Behind Cloudflare, `TrustForwardedFor` takes the first address of `X-Forwarded-For`, which a client can forge. The [Access Log](../AccessLog) and [Anti-Hammer](../AntiHammer) plug-ins have `TrustedProxies`, which only believes the headers of proxies that you name.
