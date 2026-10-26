# NetProxy Guide.

**One small service in front of everything you host.** NetProxy is a free reverse proxy, load balancer and traffic router for Windows and Linux. Point your domains and ports at one machine, and NetProxy decides where each connection goes, encrypts it, balances it, inspects it and keeps it healthy, while you manage it all from a clean desktop console, a command line or your own scripts.

It handles **HTTP, HTTPS, any TCP protocol and UDP**, runs as a normal service, and a new proxy takes a minute to set up.

---

## Why NetProxy

* **A dozen sites on one port 443.** Give each site its own host names and certificate, and let NetProxy pick the right server from the name the client asks for.
* **Free certificates, hands-off renewals.** Make a Let's Encrypt wildcard certificate with win-acme and NetProxy picks up every renewed file by itself, without a restart and without dropping a connection.
* **Not just web.** SSH, databases, game servers, DNS, WireGuard, syslog: if it is TCP or UDP, NetProxy can carry it.
* **Balance and fail over.** Spread load over several servers, or keep a standby ready for when the first one goes down.
* **See and change traffic in flight.** Rewrite HTTP headers with a rule, or write a small .NET plugin that sees the data (even the **decrypted** data of an HTTPS connection) before it is sent on.
* **Manage it your way.** A live console with charts and connection lists, a `netproxyctl` command line for scripts and automation, and a .NET library for everything else. All over an encrypted management connection, and all of it works against a remote machine.
* **No rebuilds, no restarts.** Change a proxy and only that proxy restarts. Edit the files by hand and reload, and everything you did not touch keeps running.
* **Tested.** About 450 automated tests run real proxies with real traffic.

---

## What you can do with it

### Host many websites behind a single address
You have one public IP and several sites, each on its own server. Create one proxy per site on port 443, give each its host names (`www.example.com`, `*.example.com`), and NetProxy routes every request to the right server. It reads the name from the TLS handshake (SNI) or the `Host` header, so each site can have its own certificate.

### Carve out an exception
One general proxy for `*.example.com` and `example.com`, and a second one for just `test.example.com` that goes to a different server. The most specific name always wins, so the exception needs no special setup.

### Terminate TLS in one place
Put your certificates on NetProxy and let the servers behind it speak plain HTTP, or re-encrypt to them. Either way, your servers never see a certificate renewal again.

### Balance load, survive failures
List several endpoints for one proxy and choose how connections are spread: **fail-over** (the first one that works), **round-robin** (in turn) or **balanced** (the least busy). An endpoint that stops answering is skipped for a while so nobody waits for it, and **sticky sessions** keep a client on the server it used before.

### Forward any port
Expose SSH, RDP, a database, a game server or an MQTT broker through the proxy with a **TCP** proxy. Or relay **UDP** (DNS, WireGuard, syslog, game traffic), with a session kept for each client.

### Tell your servers who is calling
Switch on `X-Forwarded-For` and `X-Forwarded-Proto` and your applications see the real client address and the original scheme, even though every request arrives from the proxy.

### Change requests and responses on the fly
Add a security header, strip a leaking one, rewrite a value: header rules apply to requests and responses in flight, for HTTP and for HTTPS that NetProxy terminates, including HTTP/2.

### Run gRPC and HTTP/2
Tick **Allow HTTP/2** and gRPC services work through the proxy, over TLS or plain.

### Protect what is behind it
Cap the connections of a proxy, cap the connections of a single client, and close idle connections automatically. Block or allow clients with a plugin.

### Extend it
Write a plugin in .NET to log, audit, block, filter or rewrite traffic. A sample plugin is included.

### Automate it
Keep your proxies as files, check them into source control, and apply them with one command. Script it, or call it from your own code.

---

## Features at a glance

| | |
| --- | --- |
| **Protocols** | HTTP, HTTPS, TCP (any protocol), UDP |
| **HTTP/2 and gRPC** | Over TLS (ALPN) or plain (h2c) |
| **TLS** | Terminate, re-encrypt or pass through untouched; a certificate per host name |
| **Routing** | Many proxies on one port by host name (exact and wildcard); a default proxy for everything else |
| **Load balancing** | Fail-over, round-robin, balanced (fewest connections) |
| **Sticky sessions** | By client address, with an expiry you choose |
| **Health** | A failed endpoint is tried last for a while, and endpoints can be switched off without deleting them |
| **Client address** | `X-Forwarded-For` and `X-Forwarded-Proto` (replace or append) |
| **Header rules** | Insert, update, upsert or delete request and response headers |
| **Certificates** | Set up once and used by name; PFX files are watched, so renewed certificates reach every proxy that uses them without a restart |
| **IP versions** | IPv4, IPv6 or both, on every address or only the ones you list |
| **Limits** | Connections per proxy, connections per client, idle timeout |
| **Plugins** | .NET plugins with service, proxy, connection and data events; data hooks see decrypted traffic |
| **Console** | Live traffic chart, proxy list and open connections, both sortable; dark theme; many languages |
| **Command line** | `netproxyctl`: apply, export, import, reload, logs, stats, connections, users |
| **API** | A .NET client library for your own tools |
| **Security** | Encrypted management connection, per-user logins, secrets stored encrypted |
| **Platforms** | Windows service, Linux service |

---

## Getting started

1. **Install the service** on the machine that will sit in front of your servers (Windows service, or Linux using the notes in `@Installers`). Open the ports you want to serve on your firewall or router.
2. **Open the console** and connect. The service listens for management on port **5854**. A new installation has one user, `administrator`, with an empty password: **set a password first.**
3. **Add a proxy** (the *Add* option of the proxy list). Choose a name, the traffic type and the port to listen on.
4. **Add an endpoint:** the address and port of the server it forwards to.
5. **Save.** The proxy starts, and the status column turns into a green check. The chart and the connection list show what is happening, live.

---

## Configuring things

### The parts of a proxy

A **proxy** listens on a port and hands every connection to one of its **endpoints**.

| Setting | What it does |
| --- | --- |
| **Name** | What the proxy is called in the console and in the files. |
| **Traffic type** | What the listener accepts: TCP, HTTP, HTTPS or UDP. |
| **Port and bindings** | The port, and whether to listen on all addresses (IPv4, IPv6 or both) or only on the addresses you list. |
| **Endpoints** | Where connections go. Each has its own address, port and traffic type, and can be switched off without deleting it. |
| **Connection pattern** | Fail-over, round-robin or balanced. |
| **Sticky sessions** | Send a client back to the endpoint it used before. |
| **Host names** | The names this proxy answers to, when several proxies share a port. |
| **HTTP header rules** | Change headers in flight. |
| **Forwarded headers** | Off, replace or append `X-Forwarded-For` and `X-Forwarded-Proto`. |
| **Plugins** | The plugins this proxy uses. |
| **Limits** | Connection limits and the idle timeout. |

### Recipe: a website with HTTPS

1. Make a PFX certificate (see *Free certificates* below).
2. Under **Settings, Certificates** add it once: a name, the PFX file on the service machine (Browse lists that machine) and its password.
3. Add a proxy of traffic type **HTTPS**, port **443**, and choose the certificate by name on its **TLS** tab.
4. Add an endpoint: your web server's address and port, traffic type **HTTP** (NetProxy terminates TLS) or **HTTPS** (it re-encrypts).
5. Optionally add a second proxy of type **HTTP** on port **80** to the same server.

### Recipe: many sites on one port

1. Create a proxy for each site, all of traffic type **HTTPS** and all on port **443**.
2. On each one's **Host names** tab, add the names it answers to: `www.example.com`, `example.com`, `*.example.com`.
3. For HTTPS, choose a certificate by name for each host name if the sites have different certificates. A host name with none uses the proxy's certificate.
4. Leave **one** proxy without host names if you want a default for names nobody claims.

How a request is matched:

* The name comes from the `Host` header (HTTP) or the TLS server name (HTTPS).
* An **exact** name beats a **wildcard**, and a longer wildcard beats a shorter one.
* A wildcard such as `*.example.com` covers everything below the domain. The root domain `example.com` is a name of its own, so list it too.

**Example.** Proxy A answers to `*.example.com` and `example.com` and goes to SERVER1. Proxy B answers to `test.example.com` and goes to SERVER2. Both are on port 443. `test.example.com` goes to SERVER2; everything else goes to SERVER1. To send deeper names such as `a.test.example.com` to SERVER2 as well, add `*.test.example.com` to proxy B.

### Recipe: load balancing

1. Create the proxy and add all of the endpoints, one for each server.
2. Choose the **connection pattern**:
   * **Fail-over** always uses the first endpoint that works, in the order you list them.
   * **Round-robin** takes them in turn.
   * **Balanced** starts with the one that has the fewest connections.
3. Switch on **sticky sessions** if a client needs to keep talking to the same server.

### Recipe: forwarding a TCP or UDP port

Choose traffic type **TCP** (SSH, RDP, databases, MQTT, anything over TCP) or **UDP** (DNS, WireGuard, syslog, games). The proxy relays the traffic as it is. For UDP, a session is kept for each client and ends after the idle timeout.

To pass **TLS through untouched**, so that the proxy cannot read it, use traffic type **TCP**.

### Recipe: the real client address

On the proxy, set **Forwarded headers** to:

* **Replace** to start fresh with the address of the client that connected to the proxy,
* **Append** when NetProxy sits behind another proxy that already sets them.

Your application then reads `X-Forwarded-For` and `X-Forwarded-Proto`.

### Recipe: gRPC and HTTP/2

Tick **Allow HTTP/2** on the *General* tab. For TLS listeners the version is negotiated with the client. Point the endpoint at a server that speaks HTTP/2 (traffic type HTTPS for TLS, or TCP/HTTP for plain h2c).

### Header rules

Add rules to a proxy to **insert**, **update**, **upsert** or **delete** headers on requests and responses. For example, add `Strict-Transport-Security` to every response, or remove a `Server` header, without touching the application.

### Limits and timeouts

On the *Advanced* tab of a proxy:

* **Max. connections:** the most connections the proxy has open at once.
* **Max. connections per client:** the most that one client address can have open.
* **Idle timeout:** close connections that carry no data for that long.

---

## Free certificates (Let's Encrypt with win-acme)

[win-acme](https://www.win-acme.com/) (`wacs.exe`) makes the PFX file NetProxy needs, no IIS involved. One command creates a wildcard certificate and writes it to a folder:

```
wacs.exe --accepttos --emailaddress info@YOURDOMAIN.com --target manual --host *.YOURDOMAIN.com,YOURDOMAIN.com --validation manual --store centralssl --centralsslstore C:\YOUR_NEW_CERT_PATH --pfxpassword "YOUR_NEW_PASSWORD" --installation none
```

* `--host *.YOURDOMAIN.com,YOURDOMAIN.com` puts the wildcard **and** the root domain on one certificate, so a single file covers everything.
* `--validation manual` shows you a DNS TXT record (`_acme-challenge.YOURDOMAIN.com`) to create at your DNS provider. Create it, wait for it to appear, press Enter.
* `--store centralssl --centralsslstore ... --pfxpassword ...` writes the PFX file into the folder you chose, protected by the password. `--installation none` skips installing it anywhere.
* In NetProxy, add the file under **Settings, Certificates** with a name of your choice and the password you gave. Then choose it by name on every proxy and host name that should use it: 50 proxies can share one wildcard certificate that you entered once.

**Renewals are picked up by themselves.** NetProxy reads each certificate file once and watches it. When win-acme (or certbot, or you) replaces one, new connections of every proxy that uses it get the new certificate seconds later. Connections that are open keep the one they started with, and nothing is restarted. A file that is still being written is retried until it can be read.

To renew a certificate, run the same command again before it expires. To renew without typing, use a win-acme DNS plugin for your DNS provider.

---

## Managing NetProxy

### The console

The Windows console shows everything live:

* A **traffic chart** for all proxies together, or for the proxy you select (sent, received and connections).
* The **proxy list**: status, name, type, port, bytes transferred, total and current connections. Click a header to sort.
* The **open connections** of the selected proxy: source and destination, protocol, version, encryption, bytes in and out, rates, start time, duration and idle time. Sortable, and right-click copies a cell or a whole row.
* Dialogs for proxies, host names, plugins, users and settings, in many languages (*Settings > Language*).

You can manage a service on another machine as easily as a local one.

### The command line: `netproxyctl`

`netproxyctl` runs on Windows, Linux and macOS and does everything the console does, over the same encrypted connection.

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

* **Apply by name.** `proxy apply` adds a proxy, or replaces the one with the same name, the way `kubectl apply` does. A proxy that is the same as what the service already has is left alone and is not restarted.
* **Great for scripts.** `--json` prints JSON, exit codes say what happened, and `export` and `import` move proxies, plugins and settings from one service to another.
* **Also:** `reload`, `logs`, `cert` (list, get, apply, remove) and `certs` (when they expire), `stats`, `connections`, `user`, `plugin`, `settings`.

### Configuration as files

Everything is stored as files in the configuration folder (`C:\ProgramData\NetProxy` on Windows, `/usr/share/NetProxy` on Linux). Edit them with any editor, check them with `netproxyctl validate`, and apply them with `netproxyctl reload`. Only what changed is applied: proxies you did not touch keep running and keep their connections. If a file has a problem, nothing is changed.

### From your own code

`NetProxy.ClientApi` is the library `netproxyctl` is built on. Use it to manage NetProxy from your own .NET tools and services.

---

## Plugins

A plugin is a .NET assembly with a class that derives from `NpPlugin`. Override the events you care about; the rest cost nothing. You build it against the [NetProxy.PluginApi](https://www.nuget.org/packages/NetProxy.PluginApi) NuGet package (`dotnet add package NetProxy.PluginApi`), which has no dependencies, so the source of NetProxy is not needed.

* **Service events:** starting, stopping, a timer tick, management logins, errors.
* **Proxy events:** starting (can say no), started, stopped, configuration changed.
* **Connection events:** refused, TLS established, connecting (can say no), endpoint connecting, connected, failed, idle timeout, disconnecting, disconnected.
* **Data events:** `OnDataReceived`, `OnDataSending`, `OnDataSent`. These sit between the two sides of a connection, so for an HTTPS proxy they see the **decrypted** data before it is encrypted again for the endpoint. A plugin can replace the data, drop it, or close the connection.

What people build with them: audit logs, IP block lists, content filters, find-and-replace in a response, request tracing, custom alerts. The included `NetProxy.SamplePlugin` logs connections, blocks clients and replaces text in traffic: a good place to start.

**Using one:**

1. In the console open *Settings > Plugins* and add the plugin: the path of its assembly on the service machine, and any settings as `name=value` lines. Put plugins in the order they should run.
2. On each proxy that should use a plugin, tick it on the proxy's **Plugins** tab.
3. Rebuilt it? Press *Reload* and the new build is loaded without restarting the service.

A plugin that throws or runs too long is logged and skipped, and the connection carries on.

---

## Running it

* **Service:** runs as a Windows service or a Linux service, starting with the machine.
* **Logs:** a rolling log file, the Windows event log and the console when you run it by hand. Level and roll-over are changed in the console and take effect immediately.
* **Security:** the management connection is encrypted before anything else is sent, logins are per user, and passwords and certificate passwords are stored encrypted.
* **Performance:** built on .NET with asynchronous networking, so a small machine can carry a lot of connections.

---

## Free to use open to improve

NetProxy is free to use. If you find it useful, you may also like [NetTunnel](https://github.com/NTDLS/NetTunnel), which routes traffic through firewalls you do not control. NetProxy is the .NET rendition of the C++ [NetRouter](https://github.com/NTDLS/NetRouter).

