using NetProxy.Hub.Common;
using NetProxy.Hub.MessageFraming;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Hub
{
    public class NpHubPacketeer
    {
        #region Events.

        public event PacketReceivedEvent? OnMessageReceived;
        public event PeerDisconnectedEvent? OnPeerDisconnected;
        public delegate void PacketReceivedEvent(NpHubPacketeer sender, NpHubPeer peer, NpHubFrame packet);
        public delegate void PeerDisconnectedEvent(NpHubPacketeer sender, NpHubPeer peer);

        #endregion

        #region Backend Variables.

        private readonly int _listenBacklog = 4;
        private Socket? _listenSocket;
        private readonly List<NpHubPeer> _peers = new();
        private AsyncCallback? _onDataReceivedCallback;

        #endregion

        #region Start/Stop.

        /// <summary>
        /// Run as a client.
        /// </summary>
        public void Start()
        {
            _listenSocket = null;
        }

        /// <summary>
        /// Run as a server.
        /// </summary>
        /// <param name="listenPort"></param>
        public void Start(int listenPort)
        {
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, listenPort);

            _listenSocket.Bind(ipLocal);
            _listenSocket.Listen(_listenBacklog);
            _listenSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
        }

        public void Stop()
        {
            Disconnect();
        }

        #endregion

        #region Connect/Disconnect.

        public void Disconnect()
        {
            try
            {
                if (_listenSocket != null)
                {
                    _listenSocket.Disconnect(false);
                }
            }
            catch
            {
            }

            _listenSocket = null;

            try
            {
                var openSockets = _peers.ToList();

                foreach (var peer in openSockets)
                {
                    try
                    {
                        peer.Socket.Disconnect(false);
                    }
                    catch
                    {
                    }
                }

                _peers.Clear();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Connect to the server.
        /// </summary>
        /// <param name="retryInBackground">If false, the client will not retry to connect if the connection fails.</param>
        /// <returns></returns>
        public bool Connect(string hostName, int port, bool retryInBackground)
        {
            IPAddress? ipAddress = NpHubSocketUtility.GetIPv4Address(hostName);
            if (ipAddress == null)
            {
                return false;
            }
            return Connect(ipAddress, port, retryInBackground);
        }

        public bool Connect(string hostName, int port)
        {
            return Connect(hostName, port, true);
        }

        public bool Connect(IPAddress ipAddress, int port)
        {
            return Connect(ipAddress, port, true);
        }

        /// <summary>
        /// Connect to the server.
        /// </summary>
        /// <param name="retryInBackground">If false, the client will not retry to connect if the connection fails.</param>
        /// <returns></returns>
        public bool Connect(IPAddress ipAddress, int port, bool retryInBackground)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                NpHubPeer peer = new NpHubPeer(socket);

                IPEndPoint ipEnd = new IPEndPoint(ipAddress, port);

                socket.Connect(ipEnd);
                if (socket != null && socket.Connected)
                {
                    lock (this)
                    {
                        _peers.Add(peer);
                    }

                    WaitForData(new NpHubSocketState(peer));
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        #endregion

        #region Socket server.

        private void OnClientConnect(IAsyncResult asyn)
        {
            lock (this)
            {
                try
                {
                    NpUtility.EnsureNotNull(_listenSocket);

                    var socket = _listenSocket.EndAccept(asyn);

                    var peer = new NpHubPeer(socket);

                    _peers.Add(peer);

                    // Let the worker Socket do the further processing for the just connected client.
                    WaitForData(new NpHubSocketState(peer));

                    _listenSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
                }
                catch
                {
                }
            }
        }

        private void WaitForData(NpHubSocketState socketState)
        {
            if (_onDataReceivedCallback == null)
            {
                _onDataReceivedCallback = new AsyncCallback(OnDataReceived);
            }

            socketState.Peer.Socket.BeginReceive(socketState.Buffer, 0, socketState.Buffer.Length, SocketFlags.None, _onDataReceivedCallback, socketState);
        }

        private void OnDataReceived(IAsyncResult asyn)
        {
            Socket? socket = null;

            try
            {
                var socketState = asyn.AsyncState as NpHubSocketState;
                NpUtility.EnsureNotNull(socketState);

                socket = socketState.Peer.Socket;

                socketState.BytesReceived = socketState.Peer.Socket.EndReceive(asyn);

                if (socketState.BytesReceived == 0)
                {
                    CleanupConnection(socketState.Peer);
                    return;
                }

                NpHubFraming.ProcessFrameBuffer(socketState, ProcessPayloadHandler);

                WaitForData(socketState);
            }
            catch (ObjectDisposedException)
            {
                CleanupConnection(socket);
                return;
            }
            catch (SocketException)
            {
                CleanupConnection(socket);
                return;
            }
            catch
            {
            }
        }

        private void ProcessPayloadHandler(NpHubSocketState state, NpHubFrame packet)
        {
            OnMessageReceived?.Invoke(this, state.Peer, packet);
        }

        private void CleanupConnection(NpHubPeer peer)
        {
            lock (this)
            {
                try
                {
                    try
                    {
                        OnPeerDisconnected?.Invoke(this, peer);

                        peer.Socket.Shutdown(SocketShutdown.Both);
                        peer.Socket.Close();
                    }
                    catch
                    {
                    }

                    _peers.Remove(peer);
                }
                catch
                {
                }
            }
        }

        private void CleanupConnection(Socket? socket)
        {
            lock (this)
            {
                try
                {
                    foreach (var peer in _peers)
                    {
                        if (peer.Socket == socket)
                        {
                            CleanupConnection(peer);
                            return;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        #endregion

        /// <summary>
        /// SendAll is a boradcast to all connected peers. To send direcrly, use SendTo()
        /// </summary>
        /// <param name="label"></param>
        /// <param name="payload"></param>
        public void SendAll(string label, string payload)
        {
            lock (this)
            {
                if (_peers.Count == 0)
                {
                    return;
                }

                byte[] packet = NpHubFraming.AssembleFrame(new NpHubFrame()
                {
                    Label = label,
                    Payload = payload
                });

                foreach (var peer in _peers)
                {
                    try
                    {
                        peer.Socket.Send(packet);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Send to is used to send data to a single peer. To boradcast use SendAll().
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="label"></param>
        /// <param name="payload"></param>
        public void SendTo(Guid peerId, string label, string payload)
        {
            lock (this)
            {
                if (_peers.Count == 0)
                {
                    return;
                }

                byte[] packet = NpHubFraming.AssembleFrame(new NpHubFrame()
                {
                    Label = label,
                    Payload = payload
                });

                foreach (var peer in _peers)
                {
                    if (peer.Id == peerId)
                    {
                        try
                        {
                            peer.Socket.Send(packet);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// SendAll is a boradcast to all connected peers. To send direcrly, use SendTo()
        /// </summary>
        /// <param name="label"></param>

        public void SendAll(string label)
        {
            SendAll(label, string.Empty);
        }

        /// <summary>
        /// Send to is used to send data to a single peer. To boradcast use SendAll().
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="label"></param>
        public void SendTo(Guid peerId, string label)
        {
            SendTo(peerId, label, string.Empty);
        }
    }
}
