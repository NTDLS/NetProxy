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
        public byte[] Buffer = new byte[Constants.DefaultBufferSize];
        public byte[] PayloadBuilder = new byte[Constants.DefaultBufferSize];
        public int PayloadBuilderLength = 0;
    }
}
