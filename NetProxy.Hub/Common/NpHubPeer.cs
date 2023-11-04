using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    public class NpHubPeer
    {
        public Socket Socket { get; set; }
        public Guid Id { get; set; }

        public NpHubPeer(Socket socket)
        {
            Id = Guid.NewGuid();
            Socket = socket;
        }
    }
}
