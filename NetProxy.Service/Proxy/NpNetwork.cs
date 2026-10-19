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
