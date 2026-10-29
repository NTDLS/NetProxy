# NetProxy Metrics

Publishes what NetProxy is doing as metrics that Prometheus collects. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.Metrics`; it builds to `NetProxy.Plugins.Metrics.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.Metrics.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.Metrics.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

Prometheus text format at `http://127.0.0.1:9464/metrics` from a small server of its own. `Listen` (`0.0.0.0:9464` for every address), `Path`, `Token` (a bearer token that the collector sends; set it when you listen on more than loopback), `HttpMetrics`.

`netproxy_connections_total`, `_connections_active`, `_connections_refused_total`, `_disconnects_total{reason}`, `_connection_duration_seconds`, `_bytes_total{direction}`, `_endpoint_connect_failures_total{endpoint}`, `_endpoints_unavailable_total`, `_proxy_up`, `_http_requests_total{code}`, `_http_request_duration_seconds` (HTTP/1.x), `_plugin_errors_total`, `_management_logins_total{result}`. Counters start at zero when the service starts.

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
