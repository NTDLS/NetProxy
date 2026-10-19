using NTDLS.Permafrost;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library
{
    /// <summary>
    /// Encrypts and decrypts the messages of a management connection with the shared secret that the console and the service
    /// agreed on (see <see cref="NTDLS.SecureKeyExchange.CompoundNegotiator"/>). Each end has its own instance for a connection.
    /// </summary>
    public class ManagementCryptographyProvider : IRmCryptographyProvider
    {
        private readonly PermafrostCipher _cipher;

        public ManagementCryptographyProvider(byte[] sharedSecret)
        {
            _cipher = new PermafrostCipher(sharedSecret, PermafrostMode.AutoReset);
        }

        public byte[] Decrypt(RmContext context, byte[] encryptedPayload)
        {
            //Cipher returns the converted bytes and leaves its argument as it was (CipherInPlace is the one that changes it).
            lock (_cipher)
            {
                return _cipher.Cipher(encryptedPayload);
            }
        }

        public byte[] Encrypt(RmContext context, byte[] payload)
        {
            lock (_cipher)
            {
                return _cipher.Cipher(payload);
            }
        }
    }
}
