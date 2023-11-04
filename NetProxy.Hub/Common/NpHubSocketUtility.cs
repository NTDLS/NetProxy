using System.Net;
using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    internal static class NpHubSocketUtility
    {
        public static IPAddress? GetIPv4Address(string hostName)
        {
            foreach (IPAddress ipAddress in Dns.GetHostAddresses(hostName))
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ipAddress;
                }
            }

            return null;
        }
    }
}
