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
        public Route Route { get; set; }
        public SocketState Peer { get; set; }
        public int BytesReceived { get; set; }
        public Socket Socket { get; set; }
        public byte[] Buffer { get; set; }
        public byte[] PayloadBuilder;
        public SecureKeyExchange.SecureKeyNegotiator KeyNegotiator { get; set; }
        public bool IsEncryptionNegotationComplete { get; set; }
         public int PayloadBuilderLength { get; set; }
        public string HttpHeaderBuilder { get; set; }
        public int MaxBufferSize { get; set; }

        private bool _IsTunnelNegotationComplete = false;
        public bool IsTunnelNegotationComplete
        {
            get
            {
                if (((this.IsIncomming && Route.BindingIsTunnel == false) && (this.Peer.IsOutgoing && Route.EndpointIsTunnel == false))
                    || ((this.IsOutgoing && Route.EndpointIsTunnel == false) && (this.Peer.IsIncomming && Route.BindingIsTunnel == false)))
                {
                    return true; //No tunneling.
                }
                else if (((this.IsOutgoing && Route.EndpointIsTunnel) && (this.Peer.IsIncomming && Route.BindingIsTunnel))
                    || ((this.IsIncomming && Route.BindingIsTunnel) && (this.Peer.IsOutgoing && Route.EndpointIsTunnel)))
                {
                    //Both connections are tunnel endpoints.
                    return _IsTunnelNegotationComplete && this.Peer._IsTunnelNegotationComplete;
                }
                else if (((this.IsOutgoing && Route.EndpointIsTunnel) && (this.Peer.IsIncomming && Route.BindingIsTunnel == false))
                    || ((this.IsIncomming && Route.BindingIsTunnel) && (this.Peer.IsOutgoing && Route.EndpointIsTunnel == false)))
                {
                    //Only the current connection is a tunnel.
                    return _IsTunnelNegotationComplete;
                }
                else if (((this.IsOutgoing && Route.EndpointIsTunnel == false) && (this.Peer.IsIncomming && Route.BindingIsTunnel))
                    || ((this.IsIncomming && Route.BindingIsTunnel == false) && (this.Peer.IsOutgoing && Route.EndpointIsTunnel)))
                {
                    //Only the peer connection is a tunnel.
                    return this.Peer._IsTunnelNegotationComplete;
                }

                //Seriously, shouldn't ever get here...
                return _IsTunnelNegotationComplete && this.Peer.IsTunnelNegotationComplete;
            }
        }

        public void SetTunnelNegotationComplete()
        {
            _IsTunnelNegotationComplete = true;
        }

        public bool UseEncryption
        {
            get
            {
                return (this.IsOutgoing && Route.EndpointIsTunnel && Route.EncryptEndpointTunnel)
                || (this.IsIncomming && Route.BindingIsTunnel && Route.EncryptBindingTunnel);
            }
        }

        public bool UseCompression
        {
            get
            {
                return (this.IsOutgoing && Route.EndpointIsTunnel && Route.CompressEndpointTunnel)
                || (this.IsIncomming && Route.BindingIsTunnel && Route.CompressBindingTunnel);
            }
        }

        public bool UsePackets
        {
            get
            {
                return (this.IsOutgoing && Route.EndpointIsTunnel)
                            || (this.IsIncomming && Route.BindingIsTunnel);
            }
        }

        public SocketState()
        {
            HttpHeaderBuilder = string.Empty;
            Buffer = new byte[Constants.DEFAULT_BUFFER_SIZE];
            PayloadBuilder = new byte[0];
        }

        public SocketState(Socket socket, int initialBufferSize)
        {
            HttpHeaderBuilder = string.Empty;
            Socket = socket;
            Buffer = new byte[initialBufferSize];
            PayloadBuilder = new byte[0];
        }
    }
}
