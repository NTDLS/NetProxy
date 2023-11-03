using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;

namespace NetProxy.Client.Classes
{
    public static class LoginPacketeerFactory
    {
        public static Packeteer? GetNewPacketeer(ConnectionInfo connectionInfo)
        {
            Packeteer packeteer = new Packeteer();

            if (packeteer.Connect(connectionInfo.ServerName, connectionInfo.Port))
            {
                var userLogin = new UserLogin()
                {
                    UserName = connectionInfo.UserName,
                    PasswordHash = Utility.Sha256(connectionInfo.Password)
                };

                packeteer.SendAll(Constants.CommandLables.GuiRequestLogin, JsonConvert.SerializeObject(userLogin));
                return packeteer;
            }

            return null;
        }
    }
}
