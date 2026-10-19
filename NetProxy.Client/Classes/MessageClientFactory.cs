using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;

namespace NetProxy.Client.Classes
{
    public static class MessageClientFactory
    {
        public static RmClient? Create(ConnectionInfo connectionInfo)
            => Create(connectionInfo, out _);

        /// <summary>
        /// Connects to the service and logs in. Returns null, with the reason, if that is not possible.
        /// </summary>
        public static RmClient? Create(ConnectionInfo connectionInfo, out string? error)
        {
            error = null;
            RmClient? client = null;

            try
            {
                client = new RmClient();
                client.Connect(connectionInfo.ServerName, connectionInfo.Port);

                var loginResult = client.Query(new QueryLogin(connectionInfo.UserName, NpUtility.Sha256(connectionInfo.Password)));
                if (loginResult.Result != true)
                {
                    throw new Exception(string.IsNullOrEmpty(loginResult.Message) ? "The user name or password is not correct." : loginResult.Message);
                }

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
    }
}
