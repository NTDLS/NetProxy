# NetProxy
Turn your machine into a TCP/IP load-balancing, tunneling, router and shape traffic with ease. Simple, clean, fast and secure!

NetProxy is the newer .NET rendition of the C++ NetRouter (https://github.com/NTDLS/NetRouter). They provide the same functionality, so let us know if you need changes to one or the other.

👀 If you like NetProxy, you may also be interested in [NetTunnel](https://github.com/NTDLS/NetTunnel), it has a lot of the same functionality with some extra flexibility. Just check it out.

This application runs as a Windows Service (or as a service on Linux) and allows you to redirect TCP/IP traffic to a host or IP address of your choosing. Unlike some dedicated hardware devices, you’ll find that load-balancing and/or fail-over configurations are easy to setup – but you’ll also find some of the more advanced options such as session persistence, buffering and even in-flight HTTP header manipulation.

As always, the service is high performance, secure, and can be remotely administered through an easy to use management interface.

If you require similar routing but through firewalls where you have no control, then be sure to check out https://github.com/NTDLS/NetTunnel

## What it does

A **proxy** listens on a port and passes every connection it accepts to one of its **endpoints**: the machines it is proxying to. Each endpoint has its own address and port, and its own traffic type. A proxy has:

| Setting | |
| --- | --- |
| **Traffic type** | What the listener accepts: Raw (any TCP protocol), HTTP, or HTTPS (the proxy terminates TLS with the certificate you give it). |
| **Bindings** | Listen on all addresses (IPv4, IPv6 or both), or only on the addresses you list. |
| **Endpoints** | Where connections go. Each endpoint is Raw, HTTP or HTTPS, and can be switched off without deleting it. |
| **Connection pattern** | Fail-over (the first endpoint that works, in the order listed), Round-robin, or Balanced (the endpoint with the fewest connections). |
| **Sticky sessions** | Send a client back to the endpoint it used before, until the session cache expiration (since it was last used) passes. |
| **HTTP header rules** | Insert, update, upsert or delete request and response headers in flight (HTTP, and HTTPS where the proxy terminates TLS). |

### Connection patterns

* **Fail-over** always tries the endpoints in the order listed, so the first one is used for as long as it works.
* **Round-robin** takes the endpoints in turn.
* **Balanced** starts with the endpoint that has the fewest connections.
* An endpoint that could not be connected to is tried last for ten seconds, so connections are not each made to wait for it to time out (it is still tried if nothing else works). Connecting to an endpoint is given ten seconds.
* With **sticky sessions** a client (by its IP address, so it is the same whichever port it connects from) goes to the endpoint it was last sent to, as long as that endpoint is enabled and is not failing. Otherwise it is sent to the next one that works.

### TLS

* **The listener** terminates TLS when the traffic type is **HTTPS**. Give it a PFX certificate (the path is on the machine that the service runs on) and its password. HTTP header rules then work on HTTPS traffic.
* **Each endpoint** chooses its own traffic type. An **HTTPS** endpoint makes the proxy start TLS with it, checking its certificate against the endpoint address (use the host name, not an IP address; tick *Ignore cert errors* for a self signed certificate). A new endpoint starts with the traffic type of the listener, which is HTTPS to HTTPS (re-encrypt) when the listener is HTTPS, and plain to plain otherwise. Any combination is allowed, for example a plain HTTP listener in front of an HTTPS endpoint.
* To pass TLS through **untouched** (the proxy cannot read it, and header rules do not apply), use the **Raw** traffic type.
* Proxies saved by an earlier version as HTTPS without a certificate are passed through as Raw, and the service logs a warning.
* Certificate passwords and the password hashes of the management users are stored encrypted in the configuration files (DPAPI on Windows, a key file next to the configuration on Linux).

### IPv6

Proxies listen on IPv4, IPv6, or both (one dual-stack listener) when they listen on all addresses, and a binding can be an IPv4 or IPv6 address (`::1` or `[::1]`). An endpoint can be an IPv6 address or a host name that has one. The management port accepts IPv6 too (*Settings > Configuration > Service*). Check the firewall for both IP versions: IPv6 addresses are often directly reachable from the internet.

### Logging

The service writes to a rolling log file (`Logs/NetProxy.log` in its installation folder by default, rolled over at a size you choose), to the Windows event log (warnings and exceptions), and to the console when it is run by hand. The level, the file and the roll-over can be changed in *Settings > Configuration > Service* and take effect immediately.

### Where things are kept

The configuration is stored in the common application data folder: `C:\ProgramData\NetProxy` on Windows, `/usr/share/NetProxy` on Linux (see the Linux install notes in `@Installers`). The `NETPROXY_CONFIG_NAME` environment variable names a different folder, so a test instance does not touch the real configuration.

### Security

* A new installation has one management user, `administrator`, with an **empty password**. Set a password first. The service logs a warning while any user has none.
* The management connection between the console and the service is **not encrypted**. Passwords are only sent as hashes, but proxy settings (including a certificate password) are sent as they are. Use it on a trusted network, or through a tunnel such as [NetTunnel](https://github.com/NTDLS/NetTunnel) or a VPN, and limit who can reach the management port (5854).

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The solution is `NetProxy.slnx`.

```
dotnet build NetProxy.slnx
```

`@Installers\Build and Compile Installers.bat` builds the Windows installer (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)) and the self contained Linux x64 and arm64 zips of the service (needs 7-Zip).

![image](https://github.com/NTDLS/NetProxy/assets/11428567/cdd3eadb-50ba-4e30-8bad-ffdf93e4d2c3)

![image](https://github.com/NTDLS/NetProxy/assets/11428567/02094550-08ed-4563-bca6-6b227da9e85a)

![image](https://github.com/NTDLS/NetProxy/assets/11428567/0a70bb69-d457-4c8a-b04c-a304ce80f312)

![image](https://github.com/NTDLS/NetProxy/assets/11428567/aab92b62-79f3-4dbb-aa4c-266bfc214b2b)

![image](https://github.com/NTDLS/NetProxy/assets/11428567/03622ca0-145e-441c-8aa0-b0bca65fd3b5)

![image](https://github.com/NTDLS/NetProxy/assets/11428567/8f64d630-1683-4e7c-b285-e1d9b6de689d)
