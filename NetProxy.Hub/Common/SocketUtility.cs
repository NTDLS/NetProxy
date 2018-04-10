using System;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    internal static class SocketUtility
    {
        public static IPAddress GetIPv4Address(string hostName)
        {
            string IP4Address = String.Empty;

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
