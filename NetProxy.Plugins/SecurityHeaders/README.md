# NetProxy Security Headers

Adds the headers that harden a web site to every response, and takes out the ones that tell too much. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.SecurityHeaders`; it builds to `NetProxy.Plugins.SecurityHeaders.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.SecurityHeaders.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.SecurityHeaders.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

Defaults: `X-Content-Type-Options: nosniff` and `Referrer-Policy: strict-origin-when-cross-origin`, and `Server`, `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` taken out. A header the site sends itself is left alone (`OnlyIfMissing=false` to override).

Off until you set them: `HstsSeconds` (+ `HstsIncludeSubDomains`, `HstsPreload`; only added on HTTPS; browsers keep it for that long, so start small), `FrameOptions`, `ContentSecurityPolicy`, `PermissionsPolicy`, `CrossOriginOpenerPolicy`, and your own `Header1=Name: value`. `ContentTypeOptions=false`, `ReferrerPolicy=...`, `RemoveHeaders=Via, X-Debug` (`RemoveHeaders=none` keeps `Server`).

A response whose head started in an earlier read, with the start already sent on, is left as it is. HTTP/2 is not changed.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
