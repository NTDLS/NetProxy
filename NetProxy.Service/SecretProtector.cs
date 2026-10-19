using NetProxy.Library.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Service
{
    /// <summary>
    /// Protects the secrets that are written to the configuration files (certificate passwords and the password hashes
    ///     used to log in), so that they are not readable in the clear when a file is copied, backed up or shared.
    ///
    /// Values are stored as "enc:dpapi:..." (Windows: DPAPI, machine scope, so that it works the same whichever
    ///     account the service or a debugger runs as, but only on that machine) or "enc:aes:..." (elsewhere: AES-256-GCM
    ///     with a random key kept in a key file next to the configuration, readable only by its owner).
    ///
    /// A value that is not protected (a file written by an older version, or a value pasted in by hand) is read as is and is
    ///     protected the next time the file is saved.
    ///
    /// This keeps the secrets out of the files. It does not defend against someone who can run code as an
    ///     administrator (or as the service account) on the same machine.
    /// </summary>
    internal static class SecretProtector
    {
        private const string DpapiPrefix = "enc:dpapi:";
        private const string AesPrefix = "enc:aes:";
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("NetProxy.SecretProtector.v1");
        private static readonly Lock _keyLock = new();
        private static byte[]? _key;

        public static bool IsProtected(string? value)
            => value != null && (value.StartsWith(DpapiPrefix, StringComparison.Ordinal) || value.StartsWith(AesPrefix, StringComparison.Ordinal));

        /// <summary>
        /// Returns the protected form of a secret. Empty values and values that are already protected are returned unchanged.
        /// </summary>
        public static string? Protect(string? secret)
        {
            if (string.IsNullOrEmpty(secret) || IsProtected(secret))
            {
                return secret;
            }

            var plain = Encoding.UTF8.GetBytes(secret);

            if (OperatingSystem.IsWindows())
            {
                return DpapiPrefix + Convert.ToBase64String(ProtectedData.Protect(plain, _entropy, DataProtectionScope.LocalMachine));
            }

            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];
            using (var aes = new AesGcm(GetKey(), TagSize))
            {
                aes.Encrypt(nonce, plain, cipher, tag, _entropy);
            }

            return AesPrefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        /// <summary>
        /// Returns the secret from its stored form. A value that is not protected is returned as it is, with
        ///     <paramref name="wasPlainText"/> set so that the caller can save it again in protected form.
        ///     A value that cannot be decrypted (for example a file copied from another machine) is logged and returned as null,
        ///     so that a lost password hash can never become an empty one that matches.
        /// </summary>
        public static string? Unprotect(string? stored, string description, out bool wasPlainText)
        {
            wasPlainText = false;

            if (string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            if (!IsProtected(stored))
            {
                wasPlainText = true;
                return stored;
            }

            try
            {
                if (stored.StartsWith(DpapiPrefix, StringComparison.Ordinal))
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        throw new PlatformNotSupportedException("The value was protected with Windows DPAPI.");
                    }

                    var bytes = ProtectedData.Unprotect(
                        Convert.FromBase64String(stored[DpapiPrefix.Length..]), _entropy, DataProtectionScope.LocalMachine);
                    return Encoding.UTF8.GetString(bytes);
                }

                var data = Convert.FromBase64String(stored[AesPrefix.Length..]);
                var nonce = data.AsSpan(0, NonceSize);
                var tag = data.AsSpan(NonceSize, TagSize);
                var cipher = data.AsSpan(NonceSize + TagSize);
                var plain = new byte[cipher.Length];
                using (var aes = new AesGcm(GetKey(), TagSize))
                {
                    aes.Decrypt(nonce, cipher, tag, plain, _entropy);
                }
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"The protected value for {description} could not be decrypted and will be ignored"
                    + $" (protected values only work on the machine that wrote them): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// The key for platforms without DPAPI. Created on first use, in a file that only its owner can read.
        /// </summary>
        private static byte[] GetKey()
        {
            lock (_keyLock)
            {
                if (_key != null)
                {
                    return _key;
                }

                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Singletons.ConfigurationName);
                var path = Path.Combine(folder, "secret.key");

                if (File.Exists(path))
                {
                    var existing = File.ReadAllBytes(path);
                    if (existing.Length != KeySize)
                    {
                        throw new InvalidDataException($"The key file '{path}' is not valid.");
                    }
                    return _key = existing;
                }

                Directory.CreateDirectory(folder);

                var key = RandomNumberGenerator.GetBytes(KeySize);
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows())
                {
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using (var stream = new FileStream(path, options))
                {
                    stream.Write(key);
                }

                return _key = key;
            }
        }
    }
}
