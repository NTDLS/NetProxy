using NetProxy.Library.MessageHubPayloads.Notifications;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;

namespace NetProxy.Client.Classes
{
    public static class MessageClientFactory
    {
        public static MessageClient? Create(ConnectionInfo connectionInfo)
        {
            try
            {
                var client = new MessageClient();
                client.Connect(connectionInfo.ServerName, connectionInfo.Port);
                client.Notify(new NotifificationRegisterLogin(connectionInfo.UserName, NpUtility.Sha256(connectionInfo.Password)));
                return client;
            }
            catch { }

            return null;
        }
    }
}
