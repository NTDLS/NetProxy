using NetProxy.Hub.Common;
using NetProxy.Library.Routing;
using NTDLS.SecureKeyExchange;
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
        public CompoundNegotiator KeyNegotiator { get; set; } = new CompoundNegotiator();
        public bool IsEncryptionNegotationComplete { get; set; }
        public int PayloadBuilderLength { get; set; }
        public string HttpHeaderBuilder { get; set; }
        public int MaxBufferSize { get; set; }

        private bool _isTunnelNegotationComplete = false;
        public bool IsTunnelNegotationComplete
        {
            get
            {
                if (((IsIncomming && Route?.BindingIsTunnel == false) && (Peer?.IsOutgoing == true && Route?.EndpointIsTunnel == false))
                    || ((IsOutgoing && Route?.EndpointIsTunnel == false) && (Peer?.IsIncomming == true && Route?.BindingIsTunnel == false)))
                {
                    return true; //No tunneling.
                }
                else if (((IsOutgoing && Route?.EndpointIsTunnel == true) && (Peer?.IsIncomming == true && Route?.BindingIsTunnel == true))
                    || ((IsIncomming == true && Route?.BindingIsTunnel == true) && (Peer?.IsOutgoing == true && Route?.EndpointIsTunnel == true)))
                {
                    //Both connections are tunnel endpoints.
                    return _isTunnelNegotationComplete == true && Peer?._isTunnelNegotationComplete == true;
                }
                else if (((IsOutgoing == true && Route?.EndpointIsTunnel == true) && (Peer?.IsIncomming == true && Route?.BindingIsTunnel == false))
                    || ((IsIncomming == true && Route?.BindingIsTunnel == true) && (Peer?.IsOutgoing == true && Route?.EndpointIsTunnel == false)))
                {
                    //Only the current connection is a tunnel.
                    return _isTunnelNegotationComplete;
                }
                else if (((IsOutgoing && Route?.EndpointIsTunnel == false) && (Peer?.IsIncomming == true && Route?.BindingIsTunnel == true))
                    || ((IsIncomming && Route?.BindingIsTunnel == false) && (Peer?.IsOutgoing == true && Route?.EndpointIsTunnel == true)))
                {
                    //Only the peer connection is a tunnel.
                    return Peer?._isTunnelNegotationComplete == true;
                }

                //Seriously, shouldn't ever get here...
                return _isTunnelNegotationComplete == true && Peer?.IsTunnelNegotationComplete == true;
            }
        }

        public void SetTunnelNegotationComplete()
        {
            _isTunnelNegotationComplete = true;
        }

        public bool UseEncryption
        {
            get
            {
                return (IsOutgoing && Route?.EndpointIsTunnel == true && Route.EncryptEndpointTunnel)
                || (IsIncomming && Route?.BindingIsTunnel == true && Route.EncryptBindingTunnel);
            }
        }

        public bool UseCompression
        {
            get
            {
                return (IsOutgoing && Route?.EndpointIsTunnel == true && Route.CompressEndpointTunnel)
                || (IsIncomming && Route?.BindingIsTunnel == true && Route.CompressBindingTunnel);
            }
        }

        public bool UsePackets
        {
            get
            {
                return (IsOutgoing && Route?.EndpointIsTunnel == true)
                    || (IsIncomming && Route?.BindingIsTunnel == true);
            }
        }

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
