NetProxy service on Linux (x64 and arm64)
=========================================

The Linux build is self contained: it carries its own .NET runtime, so nothing has to be installed first. The management
console is a Windows application: run it on a Windows machine and connect it to the service (default port 5854).

----- Unpack (use the .x64 or the .Arm64 zip to match the machine; "uname -m" says x86_64 or aarch64)
sudo apt update
sudo apt install unzip

mkdir -p ~/apps/NetProxy
unzip NetProxy.Service.Linux.x64.zip -d ~/apps/NetProxy
cd ~/apps/NetProxy
chmod +x NetProxy.Service

----- Where things are kept
The configuration (proxies, users, the key that protects the passwords in them) is kept in /usr/share/NetProxy.
The service needs to be able to write there:

sudo mkdir -p /usr/share/NetProxy
sudo chown -R $USER:$USER /usr/share/NetProxy      (use the account that runs the service)

The log is written to Logs/NetProxy.log in the folder that the service runs from (the Service tab of the console's
Server Settings can change that, and the size it is rolled over at).

----- Run it by hand
./NetProxy.Service

----- Run it as a service (systemd)
Create /etc/systemd/system/netproxy.service:

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

Then:

sudo systemctl daemon-reload
sudo systemctl enable --now netproxy
sudo systemctl status netproxy

(AmbientCapabilities lets a service that does not run as root listen on ports below 1024. By hand, use
 sudo setcap 'cap_net_bind_service=+ep' ./NetProxy.Service instead.)

----- Notes
* A new installation has one management user, "administrator", with an EMPTY password. Set a password for it first thing
  (Settings > Configuration > Users). The service writes a warning to its log for as long as a user has an empty password.
* The management port (5854) lets whoever can log in change what the service does. Do not expose it to networks you do not
  trust, and use the firewall to limit who can reach it.
* The service listens on IPv6 as well as IPv4 by default. Check the firewall for both.
* The Windows event log is not available on Linux; the log file (and, when run by hand, the console) is.
