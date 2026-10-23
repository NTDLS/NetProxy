using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Creates the listeners and the outgoing connections, so that IPv4 and IPv6 are handled in one place.
    /// </summary>
    internal static class NpNetwork
    {
        public static string NormalizeHost(string host) => NpUtility.NormalizeHost(host);

        public static string FormatHostPort(string host, int port) => NpUtility.FormatHostPort(host, port);

        /// <summary>
        /// An IPv4 address that arrived on a dual-stack socket ("::ffff:192.168.1.2") is the IPv4 address it names.
        /// </summary>
        public static IPAddress Unmap(IPAddress address)
            => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        public static IPAddress ParseAddress(string address)
        {
            var normalized = NormalizeHost(address);

            if (IPAddress.TryParse(normalized, out var parsed) == false)
            {
                throw new FormatException($"'{address}' is not a valid IPv4 or IPv6 address.");
            }

            return parsed;
        }

        /// <summary>
        /// Creates the listeners for a proxy. When it listens on all addresses that is one listener for the chosen IP version(s),
        /// otherwise one for each enabled binding (an IPv4 or an IPv6 address).
        /// </summary>
        public static List<TcpListener> CreateListeners(NpProxyConfiguration configuration)
        {
            var listeners = new List<TcpListener>();

            if (configuration.ListenOnAllAddresses)
            {
                switch (configuration.BindingProtocol)
                {
                    case BindingProtocol.IPv4:
                        listeners.Add(new TcpListener(IPAddress.Any, configuration.ListenPort));
                        break;

                    case BindingProtocol.IPv6:
                        {
                            if (Socket.OSSupportsIPv6 == false)
                            {
                                throw new NotSupportedException("The proxy listens on IPv6 but this machine does not have IPv6.");
                            }

                            var ipv6 = new TcpListener(IPAddress.IPv6Any, configuration.ListenPort);
                            ipv6.Server.DualMode = false; //IPv6 only.
                            listeners.Add(ipv6);
                        }
                        break;

                    default:
                        listeners.Add(CreateDualStackListener(configuration.ListenPort));
                        break;
                }
            }
            else
            {
                foreach (var binding in configuration.Bindings.Where(o => o.Enabled))
                {
                    listeners.Add(new TcpListener(ParseAddress(binding.Address), configuration.ListenPort));
                }
            }

            return listeners;
        }

        /// <summary>
        /// Creates and binds the UDP sockets for a proxy, one for the IP version(s) chosen when it listens on all addresses, otherwise one
        /// for each enabled binding. Throws if an address cannot be bound (for example the port is in use), with nothing left open.
        /// </summary>
        public static List<Socket> CreateUdpSockets(NpProxyConfiguration configuration)
        {
            var sockets = new List<Socket>();

            try
            {
                if (configuration.ListenOnAllAddresses)
                {
                    switch (configuration.BindingProtocol)
                    {
                        case BindingProtocol.IPv4:
                            sockets.Add(BindUdp(IPAddress.Any, configuration.ListenPort, dualMode: false));
                            break;

                        case BindingProtocol.IPv6:
                            if (Socket.OSSupportsIPv6 == false)
                            {
                                throw new NotSupportedException("The proxy listens on IPv6 but this machine does not have IPv6.");
                            }

                            sockets.Add(BindUdp(IPAddress.IPv6Any, configuration.ListenPort, dualMode: false));
                            break;

                        default:
                            try
                            {
                                sockets.Add(Socket.OSSupportsIPv6
                                    ? BindUdp(IPAddress.IPv6Any, configuration.ListenPort, dualMode: true)
                                    : BindUdp(IPAddress.Any, configuration.ListenPort, dualMode: false));
                            }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                            {
                                //IPv6 is installed but cannot be used. Listen on IPv4 only.
                                sockets.Add(BindUdp(IPAddress.Any, configuration.ListenPort, dualMode: false));
                            }
                            break;
                    }
                }
                else
                {
                    foreach (var binding in configuration.Bindings.Where(o => o.Enabled))
                    {
                        sockets.Add(BindUdp(ParseAddress(binding.Address), configuration.ListenPort, dualMode: false));
                    }
                }

                return sockets;
            }
            catch
            {
                foreach (var socket in sockets)
                {
                    socket.Dispose();
                }

                throw;
            }
        }

        private static Socket BindUdp(IPAddress address, int port, bool dualMode)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    socket.DualMode = dualMode;
                }

                //Datagrams that arrive in a burst wait here while the loop is busy, instead of being lost.
                try { socket.ReceiveBufferSize = 4 * 1024 * 1024; } catch { }

                DisableUdpConnectionReset(socket);
                socket.Bind(new IPEndPoint(address, port));
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Windows reports an ICMP "port unreachable" (a client that is gone) as an error of the next receive on a UDP socket, which
        /// would end the receive loop. It is not an error of the loop: this turns it off for a socket that talks to many clients.
        /// </summary>
        public static void DisableUdpConnectionReset(Socket socket)
        {
            if (OperatingSystem.IsWindows())
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                try { socket.IOControl(SIO_UDP_CONNRESET, [0], null); } catch { }
            }
        }

        /// <summary>
        /// A UDP socket to send to one endpoint (a host name, IPv4 address or IPv6 address, which is looked up now), so that what it
        /// receives is what that endpoint answers.
        /// </summary>
        public static async Task<Socket> ConnectUdpAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            //Dual-stack whenever the operating system has IPv6, like the connections to TCP endpoints.
            var socket = Socket.OSSupportsIPv6
                ? new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp) { DualMode = true }
                : new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                try { socket.ReceiveBufferSize = 4 * 1024 * 1024; } catch { }

                //Connecting a UDP socket only sets where it sends: it cannot find out that nothing is there, so unlike TCP there is no
                //  trying the next address of a name. One is chosen: IPv4 first, because a name that has both is far more often a
                //  service that listens on IPv4 only than the other way round.
                var normalized = NormalizeHost(host);
                var address = IPAddress.TryParse(normalized, out var literal)
                    ? literal
                    : (await Dns.GetHostAddressesAsync(normalized, timeoutSource.Token))
                        .OrderBy(o => o.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                        .FirstOrDefault() ?? throw new SocketException((int)SocketError.HostNotFound);

                await socket.ConnectAsync(address, port, timeoutSource.Token);
                return socket;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                socket.Dispose();
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:n0}s looking up {FormatHostPort(host, port)}.");
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <summary>
        /// One listener that accepts both IPv6 and IPv4 clients, or an IPv4 one when the operating system has no IPv6.
        /// </summary>
        public static TcpListener CreateDualStackListener(int port)
        {
            if (Socket.OSSupportsIPv6)
            {
                var listener = new TcpListener(IPAddress.IPv6Any, port);
                listener.Server.DualMode = true;
                return listener;
            }

            return new TcpListener(IPAddress.Any, port);
        }

        /// <summary>
        /// Connects to a host name, IPv4 address or IPv6 address, trying each address that a host name resolves to.
        /// </summary>
        public static async Task<TcpClient> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            //Created without an address family, so that it is a dual-stack socket whenever the operating system has IPv6.
            var client = new TcpClient();

            try
            {
                await client.ConnectAsync(NormalizeHost(host), port, timeoutSource.Token);
                client.NoDelay = true;
                return client;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
            {
                client.Dispose();
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:n0}s connecting to {FormatHostPort(host, port)}.");
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
    }
}
