# NetProxy service on Linux (x64 and arm64)

The Linux build is **self contained**: it carries its own .NET runtime, so nothing has to be installed first. The management
console is a Windows application: run it on a Windows machine and connect it to the service (default port **5854**).
The console and the service must be the same version, because the management connection is encrypted and a service refuses
a console that does not do that.

## Unpack

Use the `.x64` or the `.Arm64` zip to match the machine (`uname -m` says `x86_64` or `aarch64`).

```bash
sudo apt update
sudo apt install unzip

mkdir -p ~/apps/NetProxy
unzip NetProxy.Service.Linux.x64.zip -d ~/apps/NetProxy
cd ~/apps/NetProxy
chmod +x NetProxy.Service
```

## Ready-made plugins

The zip has the ready-made plugins in the `Plugins` folder, one folder each (`Plugins/AccessLog`, `Plugins/Alerts`, `Plugins/Redirect`, ...),
built for this platform. They do nothing until you add them. In the console (*Settings > Plugins*) add the plugin with a path
**relative to the folder the service runs from**, for example `Plugins/AccessLog/NetProxy.Plugins.AccessLog.dll`, leave the type
name empty, and put its settings in. Each plugin's settings are in the `README.md` of its project (`NetProxy.Plugins/<Name>`).
Keep the files of a plugin together. `Plugins/TrafficTracker` needs a CatMQ server and the ingestion service to be of any use.

## Where things are kept

The configuration (proxies, users, and the key that protects the passwords in them) is kept in `/usr/share/NetProxy`.
The service needs to be able to write there. Use the account that runs the service:

```bash
sudo mkdir -p /usr/share/NetProxy
sudo chown -R $USER:$USER /usr/share/NetProxy
```

Back up that whole folder, including `secret.key`: the certificate passwords and user passwords in the configuration cannot be
read without it. Set `NETPROXY_CONFIG_NAME` to use a different folder name (for a test instance).

The log is written to `Logs/NetProxy.log` in the folder that the service runs from. The **Service** tab of the console's
*Server Settings* changes the file, the level, and the size it is rolled over at.

## Run it by hand

```bash
./NetProxy.Service
```

## Run it as a service (systemd)

Create `/etc/systemd/system/netproxy.service`:

```ini
[Unit]
Description=NetProxy
After=network.target

[Service]
WorkingDirectory=/home/<user>/apps/NetProxy
ExecStart=/home/<user>/apps/NetProxy/NetProxy.Service
User=<user>
Restart=always
RestartSec=2
AmbientCapabilities=CAP_NET_BIND_SERVICE

[Install]
WantedBy=multi-user.target
```

Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now netproxy
sudo systemctl status netproxy
```

`AmbientCapabilities` lets a service that does not run as root listen on ports below 1024. When run by hand, use
`sudo setcap 'cap_net_bind_service=+ep' ./NetProxy.Service` instead.

To follow the log: `tail -f ~/apps/NetProxy/Logs/NetProxy.log`

## Notes

* A new installation has one management user, `administrator`, with an **empty password**. Set a password for it first thing
  (*Settings > Configuration > Users*). The service writes a warning to its log for as long as a user has an empty password.
* The management connection is encrypted, but the service is not authenticated to the console, and whoever can log in can change
  what the service does. Do not expose the management port (5854) to networks you do not trust, and use the firewall to limit who
  can reach it.
* The service listens on IPv6 as well as IPv4 by default (the management port can be set to IPv4 only in the *Service* tab).
  Check the firewall for both.
* Proxies can limit connections (in total and per client address), close idle connections, and offer HTTP/2 (and so gRPC). See the
  *Advanced* and *General* tabs of a proxy.
* The Windows event log is not available on Linux: use the log file (and, when run by hand, the console).
