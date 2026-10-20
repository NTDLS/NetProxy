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

### HTTP/2 and gRPC

Tick **Allow HTTP/2** on the *General* tab to offer HTTP/2 to clients; gRPC is HTTP/2, so this is also what gRPC needs. The HTTP header rules apply to HTTP/2 requests and responses too (names are lower case in HTTP/2, a rule for `Host` changes `:authority`, and the connection specific headers such as `Connection` cannot be added). Without header rules the data is passed through untouched.

* **HTTPS listener:** HTTP/2 is negotiated with the client (ALPN). The endpoint has to speak what the client speaks, so a client that uses HTTP/2 needs an endpoint that agrees to HTTP/2 over TLS (traffic type HTTPS) or one that accepts plain HTTP/2 (h2c, traffic type Raw or HTTP). Clients that only know HTTP/1.1 keep working.
* **HTTP listener:** a client that starts with the HTTP/2 preface (h2c with prior knowledge, which is what gRPC clients without TLS use) is detected, and the endpoint has to accept h2c. The `Upgrade: h2c` request is not supported.
* HTTP/3 is not offered.

### Limits and timeouts

On the *Advanced* tab of a proxy (0 is no limit, which is how existing proxies behave):

* **Max. connections** is the most connections the proxy has open at once.
* **Max. connections per client** is the most that one client address has open at once. Clients are told apart by IP address, so many clients behind one NAT share a limit.
* **Idle timeout (s)** closes a connection that has carried no data in either direction for that long. Keep it above the keep-alive interval of long lived connections (a gRPC stream, a database or SSH session).

A connection over a limit is closed as soon as it is accepted. The service logs a warning when that starts, and then at most once a minute.

### Logging

The service writes to a rolling log file (`Logs/NetProxy.log` in its installation folder by default, rolled over at a size you choose), to the Windows event log (warnings and exceptions), and to the console when it is run by hand. The level, the file and the roll-over can be changed in *Settings > Configuration > Service* and take effect immediately.

### Language

The console is localized with [NTDLS.Localization](https://github.com/NTDLS/NTDLS.Localization). Choose the language with *Settings > Language...* or the **Language** button on the connect window; the choice is remembered per user. The translations are in `NetProxy.Client\Translations\Translations.db`, which is created by `Localization.bat` (it runs [LocalizationScan](https://github.com/NTDLS/LocalizationScan), which needs an API key). Run it after adding or changing a user-visible string. The service itself is not localized.

### Where things are kept

The configuration is stored in the common application data folder: `C:\ProgramData\NetProxy` on Windows, `/usr/share/NetProxy` on Linux (see the Linux install notes in `@Installers`). The `NETPROXY_CONFIG_NAME` environment variable names a different folder, so a test instance does not touch the real configuration.

### Security

* A new installation has one management user, `administrator`, with an **empty password**. Set a password first. The service logs a warning while any user has none.
* The management connection between the console and the service is **encrypted**: on connecting, the two agree on a key (NTDLS.SecureKeyExchange) and everything after that, including the login and every setting, is encrypted (NTDLS.Permafrost). The service refuses a login over a connection that is not encrypted, so the console and the service have to be the same version. The connection is encrypted but the service is not authenticated to the console, so on an untrusted network still limit who can reach the management port (5854), for example with a VPN or [NetTunnel](https://github.com/NTDLS/NetTunnel).

## Screenshots

<img width="982" height="625" alt="NetProxy" src="https://github.com/user-attachments/assets/d51d04d1-deab-4bb1-bff7-3c96fe1d519e" />

<img width="488" height="500" alt="NetProxy Config" src="https://github.com/user-attachments/assets/34e6b83f-00be-4982-a66c-87cef9bb8585" />

<img width="625" height="442" alt="NetProxy General" src="https://github.com/user-attachments/assets/f3a19b64-7228-4439-b7ac-5c93f48f7157" />

<img width="625" height="442" alt="NetProxy Bindings" src="https://github.com/user-attachments/assets/069bd1ea-3f29-456a-b5a6-f22eb6b240b8" />

<img width="625" height="442" alt="NetProxy LTLS" src="https://github.com/user-attachments/assets/3388c923-897d-4ef7-ac66-444d8372c280" />

<img width="625" height="442" alt="NetProxy Headers" src="https://github.com/user-attachments/assets/bdf4875b-ea2e-4177-ad15-6c871d3add9f" />

<img width="625" height="442" alt="NetProxy Endpoints" src="https://github.com/user-attachments/assets/6c129c9b-f7b9-4433-a7ce-d94847c9ff69" />


