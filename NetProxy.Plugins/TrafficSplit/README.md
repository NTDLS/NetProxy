# NetProxy Traffic Split

Sends a share of the clients to some endpoints first: a canary release, or a blue/green switch. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.TrafficSplit`; it builds to `NetProxy.Plugins.TrafficSplit.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.TrafficSplit.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.TrafficSplit.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

Needs a NetProxy with the `OnEndpointsOrdering` event.

```
Rule1=proxy=web; endpoints=10.0.0.6:8080; percent=10
Rule2=endpoints=10.0.0.7:8080, 10.0.0.8:8080; percent=50; sticky=connection; mode=only
```

`endpoints` (address:port as in the proxy), `percent` (0 to 100, `0.5` works), `proxy` (default every proxy), `sticky=client` (default: the same client address always lands on the same side) or `connection`, `mode=prefer` (default: the chosen clients try those endpoints first and the rest the others first, each with the other as a fall-back, so nobody is turned away while any endpoint works) or `only`. Rules apply in order. The choice is made when the client connects, before its request is read, so it can not use a cookie or a header; and clients behind Cloudflare all have Cloudflare's address, so use `sticky=connection` for them. The log says each minute how the clients were split.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
