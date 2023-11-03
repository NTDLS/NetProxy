using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    public class Peer
    {
        public Socket Socket { get; set; }
        public Guid Id { get; set; }

        public Peer(Socket socket)
        {
            Id = Guid.NewGuid();
            Socket = socket;
        }
    }
}
