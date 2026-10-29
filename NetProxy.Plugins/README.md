# NetProxy Plugins

Ready-made plug-ins for [NetProxy](https://github.com/NTDLS/NetProxy). Each is a project of its own, `NetProxy.Plugins.<Name>`, that builds to its own folder and its own zip; `Common` is the code they share.

| plug-in | assembly | what it does |
|---|---|---|
| [Anti-Hammer](#anti-hammer) | `NetProxy.Plugins.AntiHammer.dll` | Bans addresses that hammer a proxy: too many requests, the same page again and again, probes for pages that are not there. |
| [Maintenance Page](#maintenance-page) | `NetProxy.Plugins.Maintenance.dll` | A "down for maintenance" page when no endpoint works, on demand, or in planned windows. |
| [Redirect](#redirect) | `NetProxy.Plugins.Redirect.dll` | Redirects by rules (http to https, old address to new) and answers ACME HTTP-01 challenges. |
| [Alerts](#alerts) | `NetProxy.Plugins.Alerts.dll` | Tells you in Discord, Slack, a webhook or e-mail when an endpoint is down, a proxy fails to start, or logins fail. |
| [Access Log](#access-log) | `NetProxy.Plugins.AccessLog.dll` | A web server style access log (common, combined, JSON) with a file per day. |
| [Geo Filter](#geo-filter) | `NetProxy.Plugins.GeoFilter.dll` | Allows or denies clients by country and network (MaxMind `.mmdb`, or Cloudflare's country header). |
| [Response Cache](#response-cache) | `NetProxy.Plugins.Cache.dll` | Keeps GET responses in memory and answers repeat requests itself. |
| [Metrics](#metrics) | `NetProxy.Plugins.Metrics.dll` | Prometheus metrics: connections, bytes, HTTP status classes, endpoint failures. |
| [Basic Auth](#basic-auth) | `NetProxy.Plugins.BasicAuth.dll` | A user name and password for the paths you choose. |
| [Security Headers](#security-headers) | `NetProxy.Plugins.SecurityHeaders.dll` | Adds HSTS, nosniff, referrer policy, CSP... and removes `Server` / `X-Powered-By`. |
| [Traffic Split](#traffic-split) | `NetProxy.Plugins.TrafficSplit.dll` | Sends a percentage of clients to chosen endpoints first (canary releases, blue/green). |

The traffic tracker (`TrafficTracker.Plugin`, with `TrafficTracker.Contracts` and the `TrafficTracker.Ingestion` service) lives in this folder too; see its own notes.

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

---

## Anti-Hammer

Bans an address, for a while, that

| does this | limit (setting) |
|---|---|
| connects too often | `MaxConnections` in `ConnectionWindowSeconds` (100 in 10 s) |
| makes too many requests | `MaxRequests` in `RequestWindowSeconds` (200 in 10 s) |
| asks for the same page again and again | `MaxSamePath` in `SamePathWindowSeconds` (120 in 60 s) |
| asks for pages that only something looking for a weakness asks for (`/.env`, `/wp-login.php`, `/phpmyadmin`...) | `MaxProbes` in `ProbeWindowMinutes` (3 in 10 min) |

A ban lasts `BanMinutes` (60). An address that is back after a ban is banned for twice as long, up to `MaxBanHours` (168), until it has behaved for `OffenceMemoryHours` (168). Bans are kept in `bans.json` in the data folder of the plug-in. A banned address is turned away when it connects; a request on a connection that is open already (and every request, behind a trusted proxy) gets a `403` with `Retry-After`.

**It starts in log-only mode.** Out of the box it logs `Would ban 198.51.100.9 for 1 hour(s): ...` and bans nobody. Look at who it names for a few days, adjust, then set `LogOnly=false`.

| setting | default | |
|---|---|---|
| `LogOnly` | `true` | Log the bans that would be made, make none. |
| `BanMinutes`, `Escalate`, `MaxBanHours`, `OffenceMemoryHours` | `60`, `true`, `168`, `168` | |
| `MaxRequests`, `RequestWindowSeconds` | `200`, `10` | |
| `MaxSamePath`, `SamePathWindowSeconds` | `120`, `60` | The path without its query string. |
| `MaxConnections`, `ConnectionWindowSeconds` | `100`, `10` | |
| `MaxProbes`, `ProbeWindowMinutes` | `3`, `10` | |
| `ProbePaths` / `ExtraProbePaths` | built in | Replace / add to the paths that count as a probe. Starts with the text, or `*text` for anywhere in the path. |
| `Allow`, `AllowPrivate` | , `true` | Never banned. |
| `TrustedProxies` | | See above. |

To lift a ban, add the address to `Allow`, or stop the service and delete its entry from `bans.json`. TCP proxies only get the connection limit; UDP is not guarded; a chunked upload is counted but the rest of that connection is not read.

## Maintenance Page

* **No endpoint works** (HTTP and HTTPS proxies): the client gets a page and a `503`, not a connection error. Needs nothing but the plug-in being enabled. This uses the `OnEndpointsUnavailable` event.
* **On demand:** `Force=true` answers every request with the page, also when the endpoints work. `Allow` lets addresses through.
* **Planned:** windows that switch it on and off by themselves.

```
Window1=2026-11-02 01:00 to 2026-11-02 03:00
Weekly1=Sat, Sun 02:00-04:00
Daily1=03:00-03:30
TimeZone=Europe/Berlin
```

Times are in `TimeZone` (UTC if not set); `22:00-02:00` goes over midnight. During a window the `Retry-After` is the time left of it.

| setting | default | |
|---|---|---|
| `Title`, `Message` | "We'll be right back", "This site is down for maintenance..." | The built page (made safe for HTML). |
| `PageFile` | | Your own HTML, from the data folder or a full path; read again within a couple of seconds of a change. |
| `ContentType`, `StatusCode`, `RetryAfterSeconds` | `text/html; charset=utf-8`, `503`, `120` | |
| `Force`, `Allow` | `false`, | |

The response has `Cache-Control: no-store` and `Connection: close`. A client that can not be given the page (HTTP/2) is closed when the site is meant to be down. TCP/UDP proxies are left alone. `Allow` is the address that connects (behind Cloudflare that is Cloudflare).

## Redirect

Rules `Rule1`, `Rule2`, ... (the first that fits wins): `match -> target [status] [keepquery]`.

```
Rule1=http://ntdls.com/page.html -> https://ntdls.com/page.html
Rule2=http://example.com/page.html -> http://example.com
Rule3=http://*/* -> https://{host}{url}
Rule4=http://*.example.com/old/* -> https://www.example.com/new/{2} 302
```

* The match is `[http://|https://]host[:port][/path]` with `*` for anything. No scheme means both, no path means every path. Host and path are not case sensitive. The query string is not part of the match.
* The target can use what the `*`s caught: `{1}` to `{9}` (in the host and then the path), and `{scheme}`, `{host}`, `{port}`, `{hostport}`, `{path}`, `{query}` (with its `?`), `{url}` (path and query). It starts with `http://`, `https://`, `{scheme}` or `/`.
* The query string is only sent on if the target says `{query}`/`{url}` or the rule says `keepquery`.
* A rule that would send the client to the URL it already asked for is skipped, so there are no loops (`http://example.com` and `http://example.com/` are the same URL).
* Status: `301` unless the rule says `302`, `303`, `307` or `308` (or `Status=` sets another default). Use `302` while you try a rule: browsers keep a `301`.
* **ACME / Let's Encrypt:** `AcmeDirectory=C:\win-acme\challenges` answers `/.well-known/acme-challenge/<token>` from that folder before the rules, so a certificate can be renewed on a port that is redirected to https. A token with a file name that could leave the folder is never answered.

The scheme is `https` when the client side of the connection is TLS; the port comes from the `Host` header. A redirect that is answered with other requests in the same read closes the connection.

## Alerts

Channels (set the ones you want):

| setting | |
|---|---|
| `Discord.Url`, `Slack.Url` | An incoming webhook. |
| `Webhook.Url`, `Webhook.Authorization` | Any URL that takes a JSON post (`{source, severity, key, title, text, timeUtc}`). The authorization (for example `Bearer abc`) is only sent to this one. |
| `Smtp.Host`, `Smtp.Port`, `Smtp.User`, `Smtp.Password`, `Smtp.From`, `Smtp.To`, `Smtp.Tls` | E-mail (`Smtp.To` is a list; TLS and port 587 by default). |

What it tells: an endpoint that could not be connected to `FailuresBeforeDown` (3) times in a row, and when it is back with how long it was down; a proxy with no working endpoint; a proxy that could not start, and when it does; errors the service reports (`AlertOnErrors`); `FailedLoginsBeforeAlert` (3) failed logins to the management interface within 10 minutes. The same thing is told once in `MinSecondsBetween` (300), with a count of how many were held back; a recovery is always told. `NotifyOnStart=true` sends a message when the plug-in starts, to test the channels. `Source` names the machine in the messages.

Alerts are sent from a task of their own. An endpoint is only found to be down when a client tries it. Put this plug-in above the Maintenance Page in the list: a plug-in that answers for a proxy with no endpoint ends that event for the plug-ins after it.

## Access Log

One line per request: client, request, status, size. `Format=combined` (default, with referrer and user agent), `common` or `json`. A file per day in `Directory` (default `logs` in the data folder of the plug-in): `access-2026-10-28.log` (`FileName`, with `{date}`), a new one when it passes `MaxFileMB` (`access-2026-10-28.1.log`), files older than `KeepDays` (30) deleted. `IgnorePathPrefixes`, `IgnoreClients`, `TrustedProxies`, `IncludeTcpProxies`. A client can not forge lines: quotes and control characters in what it sends are escaped.

## Geo Filter

```
CountryDatabase=GeoLite2-Country.mmdb
AsnDatabase=GeoLite2-ASN.mmdb
DenyCountries=CN, RU
```

Reads country and ASN databases in the MaxMind `.mmdb` format: GeoLite2 (free, with an account at maxmind.com) or the free DB-IP ones. Files are looked at once a minute, so an updated database is picked up. `AllowCountries` (only these), `DenyCountries`, `DenyAsns` / `AllowAsns` (`13335` or `AS13335`), `AllowUnknown` (`true`: an address that can not be placed is let in), `Allow` (never filtered), `AllowPrivate`, `LogOnly`. Direct clients are turned away when they connect; behind `TrustedProxies` the client of each request is found and answered with a `403`, and without a database Cloudflare's `CF-IPCountry` is used. The log says who was turned away, summed up each minute.

## Response Cache

Keeps `GET` responses in memory (`MaxMemoryMB` 64, `MaxEntries`) and answers the next client for the same URL itself, with `Age` and `X-Cache: HIT`. Kept for what the response says (`max-age`, `s-maxage`) or `DefaultTtlSeconds` (60), at most `MaxTtlSeconds`. It is careful about what it keeps: never a request with `Authorization` or a cookie (`AllowCookies=true` for sites that send cookies for everything), never a response that sets a cookie, is `private`/`no-store`/`no-cache`, or varies on anything but `Accept-Encoding`; only responses with a `Content-Length` (not chunked), up to `MaxObjectKB` (512), with a status in `Statuses` (200, 301, 308). `Hosts`, `Paths`, `ExcludePaths`, `RequireOriginTtl`, `HonorRequestCacheControl`. A client that sends the next request before the last was answered gets no cached answers. There is no purge: reload the plug-in to empty it.

## Metrics

Prometheus text format at `http://127.0.0.1:9464/metrics` from a small server of its own. `Listen` (`0.0.0.0:9464` for every address), `Path`, `Token` (a bearer token that the collector sends; set it when you listen on more than loopback), `HttpMetrics`.

`netproxy_connections_total`, `_connections_active`, `_connections_refused_total`, `_disconnects_total{reason}`, `_connection_duration_seconds`, `_bytes_total{direction}`, `_endpoint_connect_failures_total{endpoint}`, `_endpoints_unavailable_total`, `_proxy_up`, `_http_requests_total{code}`, `_http_request_duration_seconds` (HTTP/1.x), `_plugin_errors_total`, `_management_logins_total{result}`. Counters start at zero when the service starts.

## Basic Auth

```
User1=alice:a password
User2=bob:pbkdf2:200000:SALT:HASH
Paths=/admin, /tools
ExcludePaths=/admin/public
Allow=203.0.113.0/24
```

Asks for a user name and password (HTTP Basic) for `Paths` (default everything) except `ExcludePaths`, `Hosts`; `Allow` needs none. Passwords can be kept as `sha256:HEX` or `pbkdf2:ITERATIONS:SALT:HASH` so they are not in the settings; make a PBKDF2 one with PowerShell:

```powershell
$salt = [byte[]]::new(16); [Security.Cryptography.RandomNumberGenerator]::Fill($salt)
$hash = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2('the password', $salt, 200000, 'SHA256', 32)
"pbkdf2:200000:$([Convert]::ToBase64String($salt)):$([Convert]::ToBase64String($hash))"
```

`MaxFailures` (10) wrong passwords in `FailureWindowMinutes` (10) lock the address out for `LockoutMinutes` (10), also for the right password. `Realm`, `TrustedProxies`.

Basic authentication sends the password with every request: **use it on an HTTPS proxy**. It fails closed: HTTP/2, a request that can not be read as HTTP/1.x, and anything that goes wrong while checking close the connection. Untick *Allow HTTP/2* on the proxy. A connection that was upgraded (WebSocket) after a request that was let in carries on. TCP proxies are left alone.

## Security Headers

Defaults: `X-Content-Type-Options: nosniff` and `Referrer-Policy: strict-origin-when-cross-origin`, and `Server`, `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` taken out. A header the site sends itself is left alone (`OnlyIfMissing=false` to override).

Off until you set them: `HstsSeconds` (+ `HstsIncludeSubDomains`, `HstsPreload`; only added on HTTPS; browsers keep it for that long, so start small), `FrameOptions`, `ContentSecurityPolicy`, `PermissionsPolicy`, `CrossOriginOpenerPolicy`, and your own `Header1=Name: value`. `ContentTypeOptions=false`, `ReferrerPolicy=...`, `RemoveHeaders=Via, X-Debug` (`RemoveHeaders=none` keeps `Server`).

A response whose head started in an earlier read, with the start already sent on, is left as it is. HTTP/2 is not changed.

## Traffic Split

Needs a NetProxy with the `OnEndpointsOrdering` event.

```
Rule1=proxy=web; endpoints=10.0.0.6:8080; percent=10
Rule2=endpoints=10.0.0.7:8080, 10.0.0.8:8080; percent=50; sticky=connection; mode=only
```

`endpoints` (address:port as in the proxy), `percent` (0 to 100, `0.5` works), `proxy` (default every proxy), `sticky=client` (default: the same client address always lands on the same side) or `connection`, `mode=prefer` (default: the chosen clients try those endpoints first and the rest the others first, each with the other as a fall-back, so nobody is turned away while any endpoint works) or `only`. Rules apply in order. The choice is made when the client connects, before its request is read, so it can not use a cookie or a header; and clients behind Cloudflare all have Cloudflare's address, so use `sticky=connection` for them. The log says each minute how the clients were split.

---

## Building

```
dotnet test NetProxy.Plugins\Tests
```

The plug-ins are in `NetProxy.slnx` under *Plugins*. `NetProxy.PluginApi` is referenced as a project, as `NetProxy.SamplePlugin` does it: the plug-ins use `Respond`, `OnEndpointsUnavailable` and `OnEndpointsOrdering`, which are newer than the 3.1.0 package, and plug-ins outside this repository reference the NuGet package instead. `Common` (the HTTP/1 readers, address ranges, trusted proxies, settings reader) is shared by the plug-ins: its types are internal and are made visible to the plug-ins by name (`InternalsVisibleTo` in its project file), so a new plug-in has to be added there. `Publish.bat` publishes every plug-in to its own folder and zip.
