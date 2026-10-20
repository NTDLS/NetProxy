using System.Collections.Concurrent;
using System.Net;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Reverse DNS lookups for the connection list. A lookup can be slow, so it is done in the background: the name is
    /// available the next time that the address is asked for.
    /// </summary>
    internal static class NpHostNames
    {
        private const int MaxEntries = 10_000;
        private static readonly TimeSpan _lookupTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The name of each address that was looked up; empty when it has none.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _names = new();

        /// <summary>
        /// Returns the host name of the address, or null if it is not known (yet) or the address has none.
        /// </summary>
        public static string? Get(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || IPAddress.TryParse(address, out _) == false)
            {
                return null;
            }

            if (_names.TryGetValue(address, out var name))
            {
                return name.Length > 0 ? name : null;
            }

            if (_names.Count >= MaxEntries)
            {
                _names.Clear();
            }

            if (_names.TryAdd(address, string.Empty))
            {
                _ = ResolveAsync(address);
            }

            return null;
        }

        private static async Task ResolveAsync(string address)
        {
            try
            {
                using var timeout = new CancellationTokenSource(_lookupTimeout);
                var entry = await Dns.GetHostEntryAsync(address, timeout.Token);

                if (string.IsNullOrWhiteSpace(entry.HostName) == false && entry.HostName != address)
                {
                    _names[address] = entry.HostName;
                }
            }
            catch
            {
                //No name for this address. It stays empty so that it is not looked up again.
            }
        }
    }
}
