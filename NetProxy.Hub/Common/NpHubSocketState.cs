using NetProxy.Hub.MessageFraming;

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
        public byte[] Buffer = new byte[NpConstants.FrameDefaultBufferSize];
        public byte[] PayloadBuilder = new byte[NpConstants.FrameDefaultBufferSize];
        public int PayloadBuilderLength = 0;
    }
}
