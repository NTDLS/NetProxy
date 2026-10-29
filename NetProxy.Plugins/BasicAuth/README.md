# NetProxy Basic Auth

Asks for a user name and a password before a request reaches the endpoint. A plug-in for [NetProxy](https://github.com/NTDLS/NetProxy), in the project `NetProxy.Plugins.BasicAuth`; it builds to `NetProxy.Plugins.BasicAuth.dll`.

## Install

1. Run `Publish.bat` in the folder above (or take `NetProxy.Plugins.BasicAuth.zip`) and unpack it on the machine that the NetProxy service runs on, in a folder of its own. Keep its files together: next to the plug-in there is `NetProxy.Plugins.Common.dll`. The same files run on Windows and Linux.
2. In the management console open **Plugins** > **Add**, pick `NetProxy.Plugins.BasicAuth.dll`, leave the type name empty (the dll holds one plug-in), and put the settings in *Settings*, one `name=value` per line. All of them are optional unless this page says otherwise.
3. Right-click the plug-in in the list, **Select proxies...**, and tick the proxies it is for. The plug-ins of a proxy are called in the order of the list.

## Settings and behaviour

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

## Good to know

* A setting that is wrong is logged and the default is used: the plug-in still loads.
* It never gets in the way of the traffic: if it fails, the traffic goes through (or, for a plug-in that guards something, the connection is closed).
* **HTTP/1.x only.** This plug-in reads requests, and HTTP/2 is compressed, so it can not. Untick *Allow HTTP/2* on the proxy to have it work for all clients; it says in the log, once, when it meets an HTTP/2 client.
* **Behind Cloudflare or another proxy:** set `TrustedProxies` (`cloudflare`, or addresses and ranges), and the client is found in `CF-Connecting-IP` / `X-Forwarded-For`, but only when the request really comes from one of those proxies. The built in `cloudflare` list is from <https://www.cloudflare.com/ips/> as it was when this was written: add newer ranges to the setting as they appear.

## Building and testing

The tests are in `../Tests` (`dotnet test NetProxy.Plugins\Tests`). The shared code is in [`../Common`](../Common).
