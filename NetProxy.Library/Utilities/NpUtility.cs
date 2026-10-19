using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Library.Utilities
{
    public static class NpUtility
    {
        public static string Sha256(string input)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                stringBuilder.Append(hashBytes[i].ToString("x2"));
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Trims a host name or address as typed by a person and removes the square brackets that IPv6 addresses are often written with.
        /// </summary>
        public static string NormalizeHost(string host)
        {
            host = host.Trim();

            if (host.Length > 1 && host[0] == '[' && host[^1] == ']')
            {
                host = host[1..^1];
            }

            return host;
        }

        /// <summary>
        /// Formats a host name or address and port for display ("example.com:80", "[::1]:80").
        /// </summary>
        public static string FormatHostPort(string host, int port)
        {
            host = NormalizeHost(host);
            return host.Contains(':') ? $"[{host}]:{port}" : $"{host}:{port}";
        }
    }
}
