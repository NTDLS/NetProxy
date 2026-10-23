using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;
using NTDLS.SecureKeyExchange;

namespace NetProxy.Client.Classes
{
    public static class MessageClientFactory
    {
        public static RmClient? Create(ConnectionInfo connectionInfo)
            => Create(connectionInfo, out _);

        /// <summary>
        /// Connects to the service, encrypts the connection, and logs in. Returns the connection that is ready to use, or null
        /// with the reason if that is not possible.
        /// </summary>
        public static RmClient? Create(ConnectionInfo connectionInfo, out string? error)
        {
            error = null;
            RmClient? client = null;

            try
            {
                client = ConnectAndLogin(connectionInfo);
                return client;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;

                try
                {
                    client?.Disconnect();
                }
                catch
                {
                }
            }

            return null;
        }

        /// <summary>
        /// The login sequence: connect, agree on a key (so the password hash is never sent in the clear), switch to encryption, log in.
        /// The connection is closed if any of it fails.
        /// </summary>
        private static RmClient ConnectAndLogin(ConnectionInfo connectionInfo)
            => NetProxy.ClientApi.NpServiceClient.ConnectRaw(connectionInfo.ServerName, connectionInfo.Port, connectionInfo.UserName,
                NpUtility.Sha256(connectionInfo.Password), Localize("The user name or password is not correct."));
    }
}
