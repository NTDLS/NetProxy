using NetProxy.Hub;
using NetProxy.Library.MessageHubPayloads.Notifications;
using NetProxy.Library.Utilities;

namespace NetProxy.Client.Classes
{
    public static class LoginPacketeerFactory
    {
        public static HubClient? GetNewPacketeer(ConnectionInfo connectionInfo)
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
