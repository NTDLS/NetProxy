using NetProxy.Hub.Common;
using NetProxy.Library.Routing;
using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    public class SocketState
    {
        /// <summary>
        /// the conection was made by a remote peer to the proxy server.
        /// </summary>
        public bool IsIncomming { get; set; }
        /// <summary>
        /// The connection was made by the proxy to a remote peer.
        /// </summary>
        public bool IsOutgoing { get; set; }
        public Route? Route { get; set; }
        public SocketState? Peer { get; set; }
        public int BytesReceived { get; set; }
        public Socket? Socket { get; set; }
        public byte[] Buffer { get; set; }
        public byte[] PayloadBuilder;
        public bool IsEncryptionNegotationComplete { get; set; }
        public int PayloadBuilderLength { get; set; }
        public string HttpHeaderBuilder { get; set; }
        public int MaxBufferSize { get; set; }

        public SocketState()
        {
            HttpHeaderBuilder = string.Empty;
            Buffer = new byte[Constants.DefaultBufferSize];
            PayloadBuilder = Array.Empty<byte>();
        }

        public SocketState(Socket socket, int initialBufferSize)
        {
            HttpHeaderBuilder = string.Empty;
            Socket = socket;
            Buffer = new byte[initialBufferSize];
            PayloadBuilder = Array.Empty<byte>();
        }
    }
}
