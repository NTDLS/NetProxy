using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Plugins.BasicAuth
{
    /// <summary>
    /// Checks a password against what is kept for a user. Three ways of keeping it:
    ///   secret                              the password itself
    ///   sha256:HEX                          the SHA-256 of the password, in hex
    ///   pbkdf2:ITERATIONS:SALT:HASH         PBKDF2 with SHA-256, the salt and the hash in base64 (the way to keep it)
    /// A password is compared in constant time, so that how long the check takes does not give it away.
    /// </summary>
    internal static class PasswordCheck
    {
        public static bool Verify(string stored, string password)
        {
            try
            {
                if (stored.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    var expected = Convert.FromHexString(stored[7..]);
                    return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), expected);
                }

                if (stored.StartsWith("pbkdf2:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = stored.Split(':');
                    var iterations = int.Parse(parts[1]);
                    var salt = Convert.FromBase64String(parts[2]);
                    var expected = Convert.FromBase64String(parts[3]);

                    return CryptographicOperations.FixedTimeEquals(Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length), expected);
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException or ArgumentException)
            {
                return false; //A stored value that is not right matches nothing.
            }

            //Hashed first, so that the two are the same length and nothing is learnt about the length of the password.
            return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), SHA256.HashData(Encoding.UTF8.GetBytes(stored)));
        }

        /// <summary>
        /// True for a stored value that can be used (a hash that is malformed never matches, and is better reported).
        /// </summary>
        public static bool IsWellFormed(string stored)
        {
            try
            {
                if (stored.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.FromHexString(stored[7..]).Length == 32;
                }

                if (stored.StartsWith("pbkdf2:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = stored.Split(':');
                    return parts.Length == 4 && int.Parse(parts[1]) > 0 && Convert.FromBase64String(parts[2]).Length > 0 && Convert.FromBase64String(parts[3]).Length > 0;
                }

                return stored.Length > 0;
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// Makes the pbkdf2 form of a password, to put in the setting.
        /// </summary>
        public static string Hash(string password, int iterations = 200_000)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            return $"pbkdf2:{iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32))}";
        }
    }
}
