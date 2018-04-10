using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
