namespace NetProxy.Hub.Common
{
    internal class NpHubSocketState
    {
        public NpHubSocketState(NpHubPeer peer)
        {
            Peer = peer;
        }

        public bool Disconnected = false;
        public int BytesReceived;
        public NpHubPeer Peer;
        public byte[] Buffer = new byte[Constants.DefaultBufferSize];
        public byte[] PayloadBuilder = new byte[Constants.DefaultBufferSize];
        public int PayloadBuilderLength = 0;
    }
}
