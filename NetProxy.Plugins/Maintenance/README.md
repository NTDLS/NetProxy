# NetProxy Maintenance Page

Tells people that the site is down for maintenance, instead of letting their browser show a connection error. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.Maintenance`; it builds to `NetProxy.Plugins.Maintenance.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.Maintenance.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.Maintenance.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

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

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
