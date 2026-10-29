# NetProxy Access Log

Writes a web server style access log of the HTTP and HTTPS traffic of a proxy. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.AccessLog`; it builds to `NetProxy.Plugins.AccessLog.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.AccessLog.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.AccessLog.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

One line per request: client, request, status, size. `Format=combined` (default, with referrer and user agent), `common` or `json`. A file per day in `Directory` (default `logs` in the data folder of the plug-in): `access-2026-10-28.log` (`FileName`, with `{date}`), a new one when it passes `MaxFileMB` (`access-2026-10-28.1.log`), files older than `KeepDays` (30) deleted. `IgnorePathPrefixes`, `IgnoreClients`, `TrustedProxies`, `IncludeTcpProxies`. A client can not forge lines: quotes and control characters in what it sends are escaped.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges), and the client is found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
