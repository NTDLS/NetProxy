using System.Net;
using System.Net.Sockets;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// A single address or a block of them ("10.0.0.0/8", "2400:cb00::/32").
    /// </summary>
    internal sealed class AddressRange
    {
        private readonly byte[] _network;
        private readonly int _bits;

        private AddressRange(byte[] network, int bits)
        {
            _network = network;
            _bits = bits;
        }

        /// <summary>
        /// The ranges that belong to this machine or its network: loopback, private and link local.
        /// </summary>
        public static IReadOnlyList<AddressRange> Private { get; } = Parse(
            "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16", "::1/128", "fc00::/7", "fe80::/10");

        /// <summary>
        /// The ranges that Cloudflare connects from (https://www.cloudflare.com/ips/). They change rarely, but they do change: a proxy
        /// that is behind Cloudflare should check the list now and then, and can add to it with the TrustedProxies setting.
        /// </summary>
        public static IReadOnlyList<AddressRange> Cloudflare { get; } = Parse(
            "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18",
            "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
            "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
            "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32");

        private static List<AddressRange> Parse(params string[] texts)
            => texts.Select(o => TryParse(o, out var range) ? range! : throw new ArgumentException($"'{o}' is not an address range.")).ToList();

        /// <summary>
        /// An IPv4 address that arrives as an IPv6 one ("::ffff:1.2.3.4") is the IPv4 address.
        /// </summary>
        public static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        public static bool TryParseAddress(string? text, out IPAddress address)
        {
            address = IPAddress.None;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();

            //"[::1]:443" and "1.2.3.4:443" are how some proxies write an address with its port.
            if (text.StartsWith('[') && text.IndexOf(']') is > 0 and var close)
            {
                text = text[1..close];
            }
            else if (text.Count(o => o == ':') == 1)
            {
                text = text[..text.IndexOf(':')];
            }

            if (IPAddress.TryParse(text, out var parsed) == false)
            {
                return false;
            }

            address = Normalize(parsed);
            return true;
        }

        public static bool TryParse(string text, out AddressRange? range)
        {
            range = null;

            var slash = text.IndexOf('/');
            if (TryParseAddress(slash < 0 ? text : text[..slash], out var address) == false)
            {
                return false;
            }

            var network = address.GetAddressBytes();
            var bits = network.Length * 8;

            if (slash >= 0 && (int.TryParse(text[(slash + 1)..].Trim(), out bits) == false || bits < 0 || bits > network.Length * 8))
            {
                return false;
            }

            for (var i = 0; i < network.Length; i++)
            {
                var keep = Math.Clamp(bits - i * 8, 0, 8);
                network[i] &= (byte)(0xFF << (8 - keep));
            }

            range = new AddressRange(network, bits);
            return true;
        }

        public bool Contains(IPAddress address)
        {
            var bytes = Normalize(address).GetAddressBytes();
            if (bytes.Length != _network.Length)
            {
                return false;
            }

            for (var i = 0; i < bytes.Length; i++)
            {
                var keep = Math.Clamp(_bits - i * 8, 0, 8);
                if (keep == 0)
                {
                    break;
                }

                if ((bytes[i] & (byte)(0xFF << (8 - keep))) != _network[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// What an address is counted and banned as. An IPv6 client has a whole /64 to itself and picks addresses in it at will, so
        /// the /64 is the client; an IPv4 address is itself.
        /// </summary>
        public static string KeyOf(IPAddress address)
        {
            address = Normalize(address);

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return address.ToString();
            }

            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }
    }
}
