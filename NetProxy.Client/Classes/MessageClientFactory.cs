using NetProxy.Library.Payloads.ReliableMessages.Notifications;
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
                client.Notify(new NotificationRegisterLogin(connectionInfo.UserName, NpUtility.Sha256(connectionInfo.Password)));
                return client;
            }
            catch { }

            return null;
        }
    }
}
