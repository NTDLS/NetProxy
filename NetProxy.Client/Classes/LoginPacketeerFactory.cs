using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Hub;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Client.Classes
{
    public static class LoginPacketeerFactory
    {
        public static Packeteer GetNewPacketeer(ConnectionInfo connectionInfo)
        {
            Packeteer packeteer = new Packeteer();

            if (packeteer.Connect(connectionInfo.ServerName, connectionInfo.Port))
            {

                UserLogin userLogin = new UserLogin()
                {
                    UserName = connectionInfo.UserName,
                    PasswordHash = Library.Crypto.Hashing.Sha256(connectionInfo.Password)
                };

                packeteer.SendAll(Constants.CommandLables.GUIRequestLogin, JsonConvert.SerializeObject(userLogin));
                return packeteer;
            }

            return null;
        }
    }
}
