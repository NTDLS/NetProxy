using NetProxy.Library.MessageHubPayloads.Notifications;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;

namespace NetProxy.Client.Classes
{
    public static class LoginPacketeerFactory
    {
        public static HubClient? GetNewMessageHubClient(ConnectionInfo connectionInfo)
        {
            var client = new HubClient();

            try
            {
                client.Connect(connectionInfo.ServerName, connectionInfo.Port);
                client.SendNotification(new GUIRegisterLogin(connectionInfo.UserName, NpUtility.Sha256(connectionInfo.Password)));
                return client;
            }
            catch
            {
            }

            return null;
        }
    }
}
