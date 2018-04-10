using System;
using System.Net.Sockets;

namespace NetProxy.Hub.Common
{
    internal class SocketState
    {
        public SocketState(Peer peer)
        {
            this.Peer = peer;
        }

        public bool Disconnected = false;
        public int BytesReceived;
        public Peer Peer;
        public byte[] Buffer = new byte[Constants.DEFAULT_BUFFER_SIZE];
        public byte[] PayloadBuilder = new byte[Constants.DEFAULT_BUFFER_SIZE];
        public int PayloadBuilderLength = 0;
    }
}
