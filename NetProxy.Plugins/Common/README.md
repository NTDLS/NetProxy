# NetProxy Plugins: Common

The code that the plug-ins in this folder share. It is not a plug-in itself. Its types are `internal`, and `NetProxy.Plugins.Common.csproj` makes them visible, by name, to each plug-in and to the tests: **a new plug-in has to be added to the `InternalsVisibleTo` list there**. The exception is `TrafficTracker`, below, which is `public` because the traffic tracker plug-in and its ingestion service, two different programs, both use it.

| | |
|---|---|
| `RequestReader` | Finds the requests in what a client sends over an HTTP/1.x connection, in reads of any size: heads that arrive in pieces, several requests in one read, bodies skipped by `Content-Length` or chunk by chunk. With `keepBuffer` it can hold back a head that is not whole yet. |
| `HeadGate` | For the plug-ins that decide before the endpoint sees a request: holds a partial head back until it is whole, and passes it on whole. |
| `Http1StreamParser`, `ExchangeTracker` | Follow the requests and responses of a connection and turn each request into an `HttpExchange` with its status, size and time. Used by the access log and the metrics. |
| `HttpResponseHead`, `HttpResponse` | Read and write the head of a response (headers in order, repeated headers kept), and make the bytes of a response for a plug-in that answers the client itself. |
| `AddressRange`, `TrustedProxies` | Addresses and ranges (`10.0.0.0/8`, `2400:cb00::/32`), the ranges of Cloudflare, and finding the client of a request that came through a proxy that is trusted. |
| `SettingsReader` | Reads the `name=value` settings of a plug-in: numbers, flags, lists and numbered settings (`Rule1`, `Rule2`...) with a default, and a list of problems for the ones that are wrong. |
| `TrafficTracker\` (`NetProxy.Plugins.Common.TrafficTracker`) | What the [traffic tracker plug-in](../TrafficTracker) and its [ingestion service](../TrafficTracker.Ingestion) agree on: the messages that carry the web requests (`WebRequestMessages.cs`) and the queue they travel on (`TrafficQueue.cs`: default name `NetProxy.Traffic`, default port `45784`, and how it is made: persistent, with a dead-letter queue, so that nothing is lost while the service or the database is down). Both sides create the queue, so it does not matter which starts first. Needs `NTDLS.CatMQ.Shared`, which Common keeps to itself (the other plug-ins do not get it); the tracker and the service get it from the CatMQ client. |

The plug-in API itself is in [`NetProxy.PluginApi`](../../NetProxy.PluginApi).
