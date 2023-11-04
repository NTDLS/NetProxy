using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;

namespace NetProxy.Client.Classes
{
    public static class LoginPacketeerFactory
    {
        public static NpHubPacketeer? GetNewPacketeer(ConnectionInfo connectionInfo)
        {
            NpHubPacketeer packeteer = new NpHubPacketeer();

            if (packeteer.Connect(connectionInfo.ServerName, connectionInfo.Port))
            {
                var userLogin = new NpUserLogin()
                {
                    UserName = connectionInfo.UserName,
                    PasswordHash = NpUtility.Sha256(connectionInfo.Password)
                };

                packeteer.SendAll(Constants.CommandLables.GuiRequestLogin, JsonConvert.SerializeObject(userLogin));
                return packeteer;
            }

            return null;
        }
    }
}
