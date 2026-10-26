# NetProxy
Simply put, NetProxy is a genuinely full-featured reverse-proxy for Windows and Linux. On the short list, provides
TCP, UDP, HTTP, HTTPS, TLS termination, load balancing, failover, sticky sessions, HTTP header manipulation, and more.

📖 Read the [NetProxy guide](NetProxy-Guide.md) for what NetProxy can do, common use cases and step-by-step configuration recipes.

👀 If you like NetProxy, you may also be interested in [NetTunnel](https://github.com/NTDLS/NetTunnel), it has a some similar features but is designed to tunnel traffic through a single TCP connection allowing ingress without opening ports on the firewall.

*There are more screenshots at the bottom of this page.*

<img width="867" height="697" alt="image" src="https://github.com/user-attachments/assets/20540f64-3437-436d-b3ba-8a598fe4ed80" />

## What it does

A **proxy** listens on a port and passes every connection it accepts to one of its **endpoints**: the machines it is proxying to. Each endpoint has its own address and port, and its own traffic type. A proxy has:

| Setting | |
| --- | --- |
| **Traffic type** | What the listener accepts: TCP (any TCP protocol), HTTP, HTTPS (the proxy terminates TLS with the certificate you give it), or UDP. |
| **Bindings** | Listen on all addresses (IPv4, IPv6 or both), or only on the addresses you list. |
| **Endpoints** | Where connections go. Each endpoint is TCP, HTTP or HTTPS (UDP proxies relay to UDP endpoints), and can be switched off without deleting it. |
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

* **The listener** terminates TLS when the traffic type is **HTTPS**. Choose one of the service's certificates (below) for it. HTTP header rules then work on HTTPS traffic.
* **Each endpoint** chooses its own traffic type. An **HTTPS** endpoint makes the proxy start TLS with it, checking its certificate against the endpoint address (use the host name, not an IP address; tick *Ignore cert errors* for a self signed certificate). A new endpoint starts with the traffic type of the listener, which is HTTPS to HTTPS (re-encrypt) when the listener is HTTPS, and plain to plain otherwise. Any combination is allowed, for example a plain HTTP listener in front of an HTTPS endpoint.
* To pass TLS through **untouched** (the proxy cannot read it, and header rules do not apply), use the **TCP** traffic type.
* Proxies saved by an earlier version as HTTPS without a certificate are passed through as TCP, and the service logs a warning.
* **Browsing the service machine.** Wherever the console asks for a certificate file, a plugin assembly or the log file, *Browse* lists the folders of the machine that the **service** runs on, so you can pick the file even when the console runs on another machine. It lists names, sizes and dates only; the content of a file is never sent. Only a logged in management user can browse, and the same listing is available to scripts as `NpServiceClient.Browse`.
* **Certificates are set up once, and proxies use them by name.** Add each PFX file under *Settings, Certificates* (a name, the file on the machine that the service runs on, and its password). A proxy, and each of its host names, then just *chooses a certificate by name*, so a wildcard certificate that 50 proxies use is entered once. Renaming a certificate renames it in the proxies that use it, and a certificate that proxies use cannot be deleted. The list shows when each certificate expires and which proxies use it.
* **Renewed certificates are picked up by themselves.** Each PFX file is read once and watched. When one is replaced, for example by a renewal with win-acme or certbot, new connections of **every** proxy that uses it use the new certificate a few seconds later; connections that are open keep the one they started with, and nothing is restarted. A file that cannot be read (still being written, or broken) is retried for a minute and then left alone, and the previous certificate stays in use; the service logs it. Files on network shares are also checked every 30 seconds.
* The passwords of the certificates and the password hashes of the management users are stored encrypted in the configuration files (DPAPI on Windows, a key file next to the configuration on Linux).

#### Getting a free wildcard certificate (Let's Encrypt with win-acme)

[win-acme](https://www.win-acme.com/) (`wacs.exe`) can make the PFX file that NetProxy needs, without IIS. One command makes a wildcard certificate and writes it to a folder:

```
wacs.exe --accepttos --emailaddress info@YOURDOMAIN.com --target manual --host *.YOURDOMAIN.com --validation manual --store centralssl --centralsslstore C:\YOUR_NEW_CERT_PATH --pfxpassword "YOUR_NEW_PASSWORD" --installation none
```

* `--target manual --host *.YOURDOMAIN.com` asks for a wildcard certificate. Use several names (`--host *.YOURDOMAIN.com,YOURDOMAIN.com`) if you also want the root domain on the same certificate: **a wildcard does not cover the root domain**, so without it `YOURDOMAIN.com` itself is not covered (see *Sharing a port*).
* `--validation manual` is how Let's Encrypt checks that you own the domain. A wildcard can only be validated with a DNS record, so win-acme prints a `_acme-challenge.YOURDOMAIN.com` **TXT record** for you to create at your DNS provider. Create it, wait for it to be visible, then press Enter. Because you create the record by hand, this certificate does not renew by itself: run the command (or *Run renewal* in the win-acme menu) again before it expires, about every 60 days, and create the new record when asked. To renew unattended, use a win-acme DNS validation plugin for your DNS provider instead.
* `--store centralssl --centralsslstore C:\YOUR_NEW_CERT_PATH --pfxpassword "..."` writes the certificate as a PFX file in that folder, protected by that password, and `--installation none` skips installing it anywhere (no IIS needed). The file is named after the host, with the `*` of a wildcard replaced by `_` (for example `_.YOURDOMAIN.com.pfx`).
* In NetProxy, add the file under *Settings, Certificates* (the name is yours to choose, and the password is the one you gave). Proxies and host names then choose it by that name. When win-acme writes a renewed file there, NetProxy picks it up by itself (see above).
* The folder and the password are yours to choose. Keep the folder where only the service and administrators can read it.

To see or stop renewals that you no longer need, run `wacs.exe`, choose **A: Manage renewals**, type the number of the one renewal (for example `13`) so that only it is selected, and choose **C: Cancel renewal**. Do not use *Cancel* while all renewals are selected. This only stops future renewals: it does not delete the PFX file or revoke the certificate.

### UDP (DNS, WireGuard, game servers, syslog...)

Choose the **UDP** traffic type to relay datagrams. UDP has no connections, so the proxy keeps a **session** for each client (address and port): the first datagram creates it, it gets a socket of its own to its endpoint (so what the endpoint answers can be told from the answers to other clients), and it ends when the client has sent nothing for the **idle timeout**.

* **Datagrams are relayed whole** (up to 65,535 bytes) and in order. The endpoint answers the session's socket, and the answer goes back to the client from the port it sent to.
* **Idle timeout:** the setting of the proxy. For UDP `0` means 60 seconds, because a UDP session has no end of its own. Use a few seconds for DNS and minutes for a VPN or a game.
* **Limits:** *max connections* and *max connections per client* count sessions. *Max datagrams/s per client* drops what a client sends above that rate. It is off (0) by default because a VPN tunnel is one client sending tens of thousands of datagrams a second; set it for services where a client sends little (DNS, syslog). A client's queue is limited, so a flood from one address cannot use up memory, and a client that a plugin or the endpoints turned away is ignored for a few seconds instead of costing a lookup per datagram.
* **Endpoints:** round-robin, balanced and sticky sessions work per session (sticky is by client address). Fail-over is best effort: UDP cannot tell that an endpoint is down until it answers a datagram with "port unreachable", which ends the session, so that datagram is lost and the **next** client session goes to another endpoint. An endpoint given as a name that has both IPv4 and IPv6 addresses is sent to over IPv4.
* **Ports:** a UDP port belongs to one proxy (a datagram has no host name, so UDP proxies cannot share a port the way HTTP and HTTPS proxies do). A UDP proxy and a TCP proxy can use the same number.
* **Not for UDP:** host names, TLS, HTTP header rules, forwarded headers and HTTP/2. The endpoint sees the proxy's address as the client. QUIC and HTTP/3 pass through as opaque datagrams, but cannot be routed by name.
* **Abuse:** a spoofed source address can make a UDP service answer a victim. Keep the sessions per client and per proxy limited, and the datagram rate where the service allows it.

### Telling the endpoint who the client is (X-Forwarded-For)

An endpoint sees the connection come from the proxy. Set **X-Forwarded-For / -Proto** on the *HTTP Headers* tab of an HTTP or HTTPS proxy to pass the client on:

* **Set** (for a proxy that clients connect to directly): `X-Forwarded-For` is the address of the client and `X-Forwarded-Proto` is `http` or `https` (what the client used to reach the proxy). What the client sent for these is dropped, because anybody can send them.
* **Append** (for a proxy behind another proxy that sets them): the client is added to the end of `X-Forwarded-For`, and `X-Forwarded-Proto` is kept if it is there.
* Done for every request on a keep-alive connection, for HTTP/1.x and HTTP/2. Your own header rules are applied afterwards, so a rule can still change them. In a proxy file it is `"ForwardedHeaders": "Off" | "Replace" | "Append"`.

### Sharing a port (host names)

Several **HTTP** or **HTTPS** proxies can listen on the same port, if each answers to different **host names** (the *Host names* tab of the proxy).

* A host name is exact (`www.example.com`) or a wildcard for everything below a domain (`*.example.com`). **A wildcard does not cover the root domain:** `*.example.com` matches `www.example.com` and `a.b.example.com`, but not `example.com` itself, so add `example.com` as its own host name (and, for HTTPS, give it a certificate that covers it; a wildcard certificate for `*.example.com` does not cover the root domain either unless it lists `example.com` as well). The most specific match wins: an exact name before a wildcard, a longer wildcard before a shorter one.
* **Example: a general proxy and an exception.** Proxy A has the host names `*.example.com` and `example.com` and forwards to SERVER1. Proxy B has the host name `test.example.com` and forwards to SERVER2. Both listen on the same port and are both HTTP or both HTTPS. `test.example.com` goes to B, because an exact name beats a wildcard; every other name under `example.com`, and `example.com` itself, goes to A. `*.example.com` also matches deeper names such as `a.test.example.com`; to send those to B as well, add `*.test.example.com` to B, which wins because it is the longer wildcard. For HTTPS, B needs a certificate that covers `test.example.com`: a `*.example.com` wildcard certificate does, so B's host name can use the same PFX file and password as A.
* A request goes to the proxy that has its host name: the `Host` header for HTTP, or the server name (SNI) of the TLS handshake for HTTPS.
* A proxy with **no host names** is the default for the port and gets whatever no other proxy claims. Only one proxy per port can be the default. With no default, a plain HTTP request for an unknown host gets a `404`, and an unknown HTTPS server name fails the handshake.
* **HTTPS:** each host name can use its own certificate (chosen by name), for example one for `www.example.com` and another for `example.com`. A host name without one uses the certificate of the proxy. Proxies that share a port must all be HTTP or all be HTTPS.
* **TCP** (and **UDP**) proxies cannot share a port, because there is nothing to tell them apart by. The service refuses to save a proxy that conflicts with another one on the same port (and addresses).
* The proxy is chosen once per connection: the first request decides, so later requests on a keep-alive connection go to the same proxy. For the same reason HTTP/2 connections go to the proxy of the first host name (HTTPS) or to the default proxy (HTTP/2 without TLS, where the host is compressed), so clients that reuse one HTTP/2 connection for several host names can reach the wrong proxy.
* A proxy with host names answers only to them, even when it is the only one on its port (so when the proxy of another site is stopped, that site gets a 404 and its requests do not end up at a different site). A proxy without host names answers every host name.

### Managing the service without the console (`netproxyctl`)

`netproxyctl` is a cross-platform command line tool (Windows, Linux, macOS) that does what the console does, over the same encrypted management connection, so it works against a remote service too. `NetProxy.ClientApi` is the library underneath it, if you want to do the same from your own .NET code.

```
netproxyctl login --server myhost --user administrator     # asks for the password
netproxyctl status
netproxyctl proxy list
netproxyctl proxy get web > web.json                        # edit it...
netproxyctl proxy apply -f web.json --dry-run               # ...see what would change...
netproxyctl proxy apply -f web.json                         # ...and apply it
netproxyctl proxy restart web
netproxyctl user add bob --password-stdin
netproxyctl help                                            # everything else
```

* **Login:** `login` keeps the server, user and the hash of the password (not the password) in `~/.netproxy/credentials.json` for the user that ran it; on Linux and macOS only that user can read the file. `NETPROXY_SERVER`, `NETPROXY_PORT`, `NETPROXY_USER` and `NETPROXY_PASSWORD`, or `--server`, `--port`, `--user` and `--password`, take precedence, which is how scripts log in.
* **Apply by name:** `proxy apply` and `plugin apply` add the proxy or plugin, or replace the one with the same name (not case sensitive), the way `kubectl apply` does. One that is the same as what the service has is left alone, so it is not restarted. A file can hold one proxy, a list, or an export. In `proxy get` secrets are shown as `********`, and applying that keeps the secret the proxy has.
* **Editing the files by hand:** the files are in the configuration folder (see below). Edit them, run `netproxyctl validate` to check them and see what would change, then `netproxyctl reload` (or `kill -HUP` the service on Linux and macOS). Reload applies only what changed: proxies you did not touch keep running and keep their connections, and nothing is changed if the files have a problem (including a file with no proxies, which is what a file that could not be read looks like). Plain text passwords you typed into the files are encrypted the next time the service saves. The management port only changes when the service restarts. Do not edit the files while a console is changing the same things: the service rewrites them on every change.
* **Scripts:** `--json` prints JSON, and the exit code is 0 when it was done, 1 when it was not, 2 for a wrong command line and 3 when it could not connect or log in. `export` and `import` move the proxies, plugins and settings between services (users are not included).
* Also: `reload`, `logs`, `cert` (list, get, apply and remove certificates) and `certs` (when they expire), `stats`, `connections` (and `connections close`), `user ...`, `plugin ...`, `settings ...`.

### Plugins

NetProxy can be extended with .NET plugins. A plugin is an assembly with a class that derives from `NpPlugin` (the `NetProxy.PluginApi` project, which has no dependencies). `NetProxy.SamplePlugin` is a small working example: it logs connections, can block clients, and can find and replace text in the traffic.

* **Setting up:** plugins belong to the service. Add them in the console under *Settings, Plugins* (assembly path on the service machine, optional class name, settings as `name=value` lines, a time limit per event), and put them in the order they are called in. A proxy then **opts in** on its *Plugins* tab: a proxy uses no plugins unless it ticks them.
* **Writing one:** derive from `NpPlugin`, mark the class with `[NpPlugin("Name")]`, and override the events you want. Events you do not override cost nothing. Reference `NetProxy.PluginApi` without copying it next to your assembly (`Private=false`), because the service supplies it. Plugins are loaded in their own load context, so a new build can be loaded with *Reload* without restarting the service.
* **Events:**
  * Service: `OnServiceStarting`, `OnServiceStopping`, `OnTick` (when `TickInterval` is set), `OnManagementLogin`, `OnError`.
  * Proxy: `OnProxyStarting` (can say no), `OnProxyStarted`, `OnProxyStartFailed`, `OnProxyStopping`, `OnProxyStopped`, `OnProxyConfigurationChanged`.
  * Connection: `OnConnectionRefused`, `OnClientTlsEstablished`, `OnConnecting` (can say no), `OnEndpointConnecting` (can say no, and the next endpoint is tried), `OnEndpointConnected`, `OnEndpointConnectFailed`, `OnConnected`, `OnIdleTimeout`, `OnDisconnecting`, `OnDisconnected`.
  * Data: `OnDataReceived`, `OnDataSending` and `OnDataSent`.
* **Data events** sit between the two sides of a connection, so for an HTTPS proxy that terminates TLS they see the **decrypted** data (`ReceivedEncrypted` says it was encrypted on the wire) before it is encrypted again for the endpoint (`WillBeSentEncrypted`). `OnDataReceived` is called with what was read, before the HTTP header rules; `OnDataSending` is called after them, right before the write. Both can `Replace` the data (any length), `Drop` it, or `CloseConnection`. A read is whatever arrived together, not a message. A TCP proxy that passes TLS through only has encrypted bytes to give to a plugin.
* **UDP:** on a UDP proxy a client's session is a connection (it is "connecting" when its first datagram arrives and "disconnected" when the session ends), and every datagram is a data event, so `OnDataReceived` and `OnDataSending` get whole datagrams: a datagram is a message, unlike a read. Nothing is encrypted by the proxy, so `ReceivedEncrypted` and `WillBeSentEncrypted` are false.
* **Order and safety:** the plugins of a proxy are called in order, and each gets the data as the one before left it. An event that can say no stops at the first plugin that does. A plugin that throws, or takes longer than its time limit, is logged and skipped for that event; the connection carries on.
* Not in this version: events for individual HTTP requests and responses (use the data events), and for certificates that are about to expire.

### IPv6

Proxies listen on IPv4, IPv6, or both (one dual-stack listener) when they listen on all addresses, and a binding can be an IPv4 or IPv6 address (`::1` or `[::1]`). An endpoint can be an IPv6 address or a host name that has one. The management port accepts IPv6 too (*Settings > Configuration > Service*). Check the firewall for both IP versions: IPv6 addresses are often directly reachable from the internet.

### HTTP/2 and gRPC

Tick **Allow HTTP/2** on the *General* tab to offer HTTP/2 to clients; gRPC is HTTP/2, so this is also what gRPC needs. The HTTP header rules apply to HTTP/2 requests and responses too (names are lower case in HTTP/2, a rule for `Host` changes `:authority`, and the connection specific headers such as `Connection` cannot be added). Without header rules the data is passed through untouched.

* **HTTPS listener:** HTTP/2 is negotiated with the client (ALPN). The endpoint has to speak what the client speaks, so a client that uses HTTP/2 needs an endpoint that agrees to HTTP/2 over TLS (traffic type HTTPS) or one that accepts plain HTTP/2 (h2c, traffic type TCP or HTTP). Clients that only know HTTP/1.1 keep working.
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

### Where things are kept

The configuration is stored in the common application data folder: `C:\ProgramData\NetProxy` on Windows, `/usr/share/NetProxy` on Linux (see the Linux install notes in `@Installers`). The `NETPROXY_CONFIG_NAME` environment variable names a different folder, so a test instance does not touch the real configuration.

### Security

* A new installation has one management user, `administrator`, with an **empty password**. Set a password first. The service logs a warning while any user has none.
* The management connection between the console and the service is **encrypted**: on connecting, the two agree on a key (NTDLS.SecureKeyExchange) and everything after that, including the login and every setting, is encrypted (NTDLS.Permafrost). The service refuses a login over a connection that is not encrypted, so the console and the service have to be the same version. The connection is encrypted but the service is not authenticated to the console, so on an untrusted network still limit who can reach the management port (5854), for example with a VPN or [NetTunnel](https://github.com/NTDLS/NetTunnel).

## Screenshots

<img width="488" height="500" alt="NetProxy Config" src="https://github.com/user-attachments/assets/34e6b83f-00be-4982-a66c-87cef9bb8585" />

<img width="625" height="442" alt="NetProxy General" src="https://github.com/user-attachments/assets/f3a19b64-7228-4439-b7ac-5c93f48f7157" />

<img width="625" height="442" alt="NetProxy Bindings" src="https://github.com/user-attachments/assets/069bd1ea-3f29-456a-b5a6-f22eb6b240b8" />

<img width="625" height="442" alt="NetProxy LTLS" src="https://github.com/user-attachments/assets/3388c923-897d-4ef7-ac66-444d8372c280" />

<img width="625" height="442" alt="NetProxy Headers" src="https://github.com/user-attachments/assets/bdf4875b-ea2e-4177-ad15-6c871d3add9f" />

<img width="625" height="442" alt="NetProxy Endpoints" src="https://github.com/user-attachments/assets/6c129c9b-f7b9-4433-a7ce-d94847c9ff69" />


