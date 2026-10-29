# NetProxy Anti-Hammer

Bans the addresses that hammer a proxy, for a while. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.AntiHammer`; it builds to `NetProxy.Plugins.AntiHammer.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.AntiHammer.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.AntiHammer.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

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

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges), and the client is found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
