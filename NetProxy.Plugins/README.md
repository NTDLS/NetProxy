# NetProxy Plugins

Ready-made plug-ins for [NetProxy](https://github.com/NTDLS/NetProxy). Each is a project of its own, `NetProxy.Plugins.<Name>`, that builds to its own folder and its own zip; `Common` is the code they share.

| plug-in | assembly | what it does |
|---|---|---|
| [Anti-Hammer](AntiHammer/README.md) | `NetProxy.Plugins.AntiHammer.dll` | Bans addresses that hammer a proxy: too many requests, the same page again and again, probes for pages that are not there. |
| [Maintenance Page](Maintenance/README.md) | `NetProxy.Plugins.Maintenance.dll` | A "down for maintenance" page when no endpoint works, on demand, or in planned windows. |
| [Redirect](Redirect/README.md) | `NetProxy.Plugins.Redirect.dll` | Redirects by rules (http to https, old address to new) and answers ACME HTTP-01 challenges. |
| [Alerts](Alerts/README.md) | `NetProxy.Plugins.Alerts.dll` | Tells you in Discord, Slack, a webhook or e-mail when an endpoint is down, a proxy fails to start, or logins fail. |
| [Access Log](AccessLog/README.md) | `NetProxy.Plugins.AccessLog.dll` | A web server style access log (common, combined, JSON) with a file per day. |
| [Geo Filter](GeoFilter/README.md) | `NetProxy.Plugins.GeoFilter.dll` | Allows or denies clients by country and network (MaxMind `.mmdb`, or Cloudflare's country header). |
| [Response Cache](Cache/README.md) | `NetProxy.Plugins.Cache.dll` | Keeps GET responses in memory and answers repeat requests itself. |
| [Metrics](Metrics/README.md) | `NetProxy.Plugins.Metrics.dll` | Prometheus metrics: connections, bytes, HTTP status classes, endpoint failures. |
| [Basic Auth](BasicAuth/README.md) | `NetProxy.Plugins.BasicAuth.dll` | A user name and password for the paths you choose. |
| [Security Headers](SecurityHeaders/README.md) | `NetProxy.Plugins.SecurityHeaders.dll` | Adds HSTS, nosniff, referrer policy, CSP... and removes `Server` / `X-Powered-By`. |
| [Traffic Split](TrafficSplit/README.md) | `NetProxy.Plugins.TrafficSplit.dll` | Sends a percentage of clients to chosen endpoints first (canary releases, blue/green). |

The traffic tracker is here too: [`TrafficTracker.Plugin`](TrafficTracker.Plugin/README.md), the [`TrafficTracker.Ingestion`](TrafficTracker.Ingestion/README.md) service that writes what it sees to a database, and the [`TrafficTracker.Contracts`](TrafficTracker.Contracts/README.md) they share. [`Common`](Common/README.md) is the code that the plug-ins share.

Every project has a README of its own with its settings: follow the link in the table.

## Install

1. Run `Publish.bat` (or take the zip of the plug-in you want) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`, and for the Geo Filter `MaxMind.Db.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**. Pick the plug-in's dll (`NetProxy.Plugins.AccessLog.dll`, ...), leave the type name empty (each dll holds one plug-in), and put the settings in *Settings* (one `name=value` per line).
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for.

The plug-ins of a proxy are called in the order of the list in the console. Put the ones that answer or turn clients away (Anti-Hammer, Geo Filter, Basic Auth, Redirect, Maintenance) above the ones that only watch (Access Log, Metrics, Alerts), except that Alerts goes above Maintenance (see Alerts).

## Things all of them have in common

* **HTTP/1.x only.** A plug-in that has to read requests can not read HTTP/2 (it is compressed). Untick *Allow HTTP/2* on the proxy to have them work for all clients. Each one says in the log, once, when it meets an HTTP/2 client. The ones that guard something (Basic Auth, Maintenance when the site is meant to be down) close an HTTP/2 connection and do not let it through.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges). The client is then found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.
* **A setting that is wrong** is logged and the default is used. The plug-in still loads.
* **They never get in the way of the traffic**: a plug-in that fails lets the traffic through (or, for the ones that guard, closes it).
* IPv6 clients are counted by their /64.

## Building

```
dotnet test NetProxy.Plugins\Tests
```

The plug-ins are in `NetProxy.slnx` under *Plugins*. `NetProxy.PluginApi` is referenced as a project, as `NetProxy.SamplePlugin` does it: the plug-ins use `Respond`, `OnEndpointsUnavailable` and `OnEndpointsOrdering`, which are newer than the 3.1.0 package, and plug-ins outside this repository reference the NuGet package instead. `Common` (the HTTP/1 readers, address ranges, trusted proxies, settings reader) is shared by the plug-ins: its types are internal and are made visible to the plug-ins by name (`InternalsVisibleTo` in its project file), so a new plug-in has to be added there. `Publish.bat` publishes every plug-in to its own folder and zip.
