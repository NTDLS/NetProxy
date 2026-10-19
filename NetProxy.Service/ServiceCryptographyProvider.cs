using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    /// <summary>
    /// The cryptography provider of the management server: every connection has its own key, and a connection is encrypted from the
    /// moment its console tells the service to apply it (before that, the key exchange and that request are not encrypted).
    /// </summary>
    internal class ServiceCryptographyProvider(NpServiceManager serviceManager) : IRmCryptographyProvider
    {
        public byte[] Decrypt(RmContext context, byte[] encryptedPayload)
            => serviceManager.TryGetAppliedCryptography(context.ConnectionId, out var provider)
                ? provider.Decrypt(context, encryptedPayload) : encryptedPayload;

        public byte[] Encrypt(RmContext context, byte[] payload)
            => serviceManager.TryGetAppliedCryptography(context.ConnectionId, out var provider)
                ? provider.Encrypt(context, payload) : payload;
    }
}
