# NetProxy Redirect

Sends clients somewhere else by rules, and answers ACME HTTP-01 challenges. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.Redirect`; it builds to `NetProxy.Plugins.Redirect.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.Redirect.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.Redirect.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

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

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges), and the client is found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
