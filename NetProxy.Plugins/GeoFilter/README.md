# NetProxy Geo Filter

Lets clients in or keeps them out by where they are: their country and their network. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.GeoFilter`; it builds to `NetProxy.Plugins.GeoFilter.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.GeoFilter.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll` and `MaxMind.Db.dll` (reads the databases). The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.GeoFilter.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

```
CountryDatabase=GeoLite2-Country.mmdb
AsnDatabase=GeoLite2-ASN.mmdb
DenyCountries=CN, RU
```

Reads country and ASN databases in the MaxMind `.mmdb` format: GeoLite2 (free, with an account at maxmind.com) or the free DB-IP ones. Files are looked at once a minute, so an updated database is picked up. `AllowCountries` (only these), `DenyCountries`, `DenyAsns` / `AllowAsns` (`13335` or `AS13335`), `AllowUnknown` (`true`: an address that can not be placed is let in), `Allow` (never filtered), `AllowPrivate`, `LogOnly`. Direct clients are turned away when they connect; behind `TrustedProxies` the client of each request is found and answered with a `403`, and without a database Cloudflare's `CF-IPCountry` is used. The log says who was turned away, summed up each minute.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges), and the client is found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
