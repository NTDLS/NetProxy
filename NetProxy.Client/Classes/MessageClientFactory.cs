using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;

namespace NetProxy.Client.Classes
{
    public static class MessageClientFactory
    {
        public static RmClient? Create(ConnectionInfo connectionInfo)
        {
            try
            {
                var client = new RmClient();
                client.Connect(connectionInfo.ServerName, connectionInfo.Port);

                var loginResult = client.Query(new QueryLogin(connectionInfo.UserName, NpUtility.Sha256(connectionInfo.Password))).Result;
                if (loginResult.Result != true)
                {
                    throw new Exception("Login failed.");
                }

                return client;
            }
            catch { }

            return null;
        }
    }
}
