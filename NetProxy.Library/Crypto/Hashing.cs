using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Library.Crypto
{
    public static class Hashing
    {
        public static string Sha256(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            var crypt = new SHA256Managed();
            StringBuilder hash = new StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(value), 0, Encoding.UTF8.GetByteCount(value));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static byte[] Sha256Bytes(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            var crypt = new SHA256Managed();
            StringBuilder hash = new StringBuilder();
            return crypt.ComputeHash(Encoding.UTF8.GetBytes(value), 0, Encoding.UTF8.GetByteCount(value));
        }

        public static string Sha1(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            var crypt = new SHA1Managed();
            StringBuilder hash = new StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(value), 0, Encoding.UTF8.GetByteCount(value));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static byte[] Sha1Bytes(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            var crypt = new SHA1Managed();
            StringBuilder hash = new StringBuilder();
            return crypt.ComputeHash(Encoding.UTF8.GetBytes(value), 0, Encoding.UTF8.GetByteCount(value));
        }
    }
}
