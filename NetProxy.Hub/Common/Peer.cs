using System;
using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    public class Peer
    {
        public Socket Socket { get; set; }
        public Guid Id = Guid.NewGuid();

        public Peer(Socket socket)
        {
            this.Socket = socket;
        }
    }
}
