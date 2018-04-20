using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Win32;
using static NetProxy.Library.Constants;

namespace NetProxy.Service.Routing
{
    public class Router
    {
        #region Backend Variables.

        private MemoryCache stickySessionCache = new MemoryCache("NetProxy.Service.Routing.Router.StickySessionCache");
        public RouterStatistics Stats = new RouterStatistics();
        private int lastRoundRobinIndex = 0;
        private Route route;
        private Socket listenSocket = null;
        private List<SocketState> connections = new List<SocketState>();
        private AsyncCallback OnDataReceivedCallback;

        public int CurrentConnectionCount
        {
            get
            {
                lock (connections)
                {
                    return connections.Count;
                }
            }
        }

        public Route Route
        {
            get
            {
                return route;
            }
        }

        #endregion

        public Router(Route route)
        {
            this.route = route;
        }

        public bool IsRunning
        {
            get
            {
                return listenSocket != null;
            }
        }

        #region Start/Stop.

        public bool Start()
        {
            if (IsRunning)
            {
                return true;
            }

            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());

                if (route.BindingProtocal == BindingProtocal.IPv6)
                {
                    listenSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                }
                else if (route.BindingProtocal == BindingProtocal.IPv4)
                {
                    listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (route.ListenOnAllAddresses)
                {
                    if (route.BindingProtocal == BindingProtocal.IPv6)
                    {
                        IPEndPoint ipLocal = new IPEndPoint(IPAddress.IPv6Any, route.ListenPort);
                        listenSocket.Bind(ipLocal);
                    }
                    else if (route.BindingProtocal == BindingProtocal.IPv4)
                    {
                        IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, route.ListenPort);
                        listenSocket.Bind(ipLocal);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    foreach (var binding in route.Bindings)
                    {
                        if (binding.Enabled)
                        {
                            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(binding.Address), route.ListenPort);
                            listenSocket.Bind(ipLocal);
                        }
                    }
                }

                listenSocket.Listen(route.AcceptBacklogSize);
                listenSocket.BeginAccept(new AsyncCallback(OnConnectionAccepted), null);

                return true;
            }
            catch (Exception ex)
            {
                if (listenSocket != null)
                {
                    try
                    {
                        listenSocket.Close();
                    }
                    catch
                    {
                    }
                }
                listenSocket = null;

                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to start route.",
                    Exception = ex
                });

                throw;
            }

            //return false;
        }

        public void Stop()
        {
            if (IsRunning == false)
            {
                return;
            }

            try
            {
                if (listenSocket != null)
                {
                    listenSocket.Close();
                    listenSocket = null;
                }

                lock (connections)
                {
                    while (connections.Count > 0)
                    {
                        try
                        {
                            CleanupConnection(connections[0]);
                        }
                        catch
                        {
                        }
                    }

                    connections.Clear();
                }
            }
            catch (Exception ex)
            {
                listenSocket = null;

                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to stop route.",
                    Exception = ex
                });
                throw;
            }
        }

        #endregion

        #region Connect / Accept.

        private void OnConnectionAccepted(IAsyncResult asyn)
        {
            try
            {
                if (listenSocket != null)
                {
                    Stats.TotalConnections++;

                    Socket socket = listenSocket.EndAccept(asyn);
                    SocketState connection = new SocketState(socket, route.InitialBufferSize);

                    connection.Route = route;
                    connection.IsIncomming = true;

                    if (route.EncryptBindingTunnel)
                    {
                        connection.KeyNegotiator = new SecureKeyExchange.SecureKeyNegotiator();

                        SendPacketEnvelope(connection, new PacketEnvelope
                        {
                            Label = IntraServiceLables.ApplyNegotiationToken,
                            Payload = connection.KeyNegotiator.GenerateNegotiationToken()
                        });
                    }
                    else if (route.BindingIsTunnel)
                    {
                        SendPacketEnvelope(connection, new PacketEnvelope
                        {
                            Label = IntraServiceLables.TunnelNegotationComplete,
                        });
                    }

                    (new Thread(EstablishPeerConnection)).Start(connection);

                    listenSocket.BeginAccept(new AsyncCallback(OnConnectionAccepted), null);
                }
            }
            catch
            {
                //Discard.
            }
        }

        public SocketState Connect(string hostName, int port)
        {
            IPAddress ipAddress = GetIPAddress(hostName);
            if (ipAddress != null)
            {
                return Connect(ipAddress, port);
            }
            return null;
        }

        public SocketState Connect(IPAddress ipAddress, int port)
        {
            try
            {
                Socket socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipEnd = new IPEndPoint(ipAddress, port);

                socket.Connect(ipEnd);
                if (socket.Connected)
                {
                    Stats.TotalConnections++;
                    var connection = new SocketState(socket, route.InitialBufferSize);

                    connection.IsOutgoing = true;
                    connection.Route = route;

                    if (route.EndpointIsTunnel && route.EncryptEndpointTunnel == false)
                    {
                        SendPacketEnvelope(connection, new PacketEnvelope
                        {
                            Label = IntraServiceLables.TunnelNegotationComplete,
                        });
                    }

                    return connection;
                }
            }
            catch
            {
            }

            return null;
        }

        private void ProcessPeerCommand(SocketState connection, PacketEnvelope envelope)
        {
            if (envelope.Label == IntraServiceLables.ApplyNegotiationToken)
            {
                connection.KeyNegotiator = new SecureKeyExchange.SecureKeyNegotiator();
                byte[] replyToken = connection.KeyNegotiator.ApplyNegotiationToken(envelope.Payload);

                SendPacketEnvelope(connection, new PacketEnvelope
                {
                    Label = IntraServiceLables.ApplyResponseNegotiationToken,
                    Payload = replyToken
                });

                SendPacketEnvelope(connection, new PacketEnvelope
                {
                    Label = IntraServiceLables.EncryptionNegotationComplete
                });
            }
            else if (envelope.Label == IntraServiceLables.ApplyResponseNegotiationToken)
            {
                connection.KeyNegotiator.ApplyNegotiationResponseToken(envelope.Payload);

                SendPacketEnvelope(connection, new PacketEnvelope
                {
                    Label = IntraServiceLables.EncryptionNegotationComplete
                });
            }
            else if (envelope.Label == IntraServiceLables.EncryptionNegotationComplete)
            {
                connection.IsEncryptionNegotationComplete = true;
                //Console.WriteLine("--{0} Shared Secret: {1}", connection.Route.Name, connection.KeyNegotiator.SharedSecretString);

                string sharedSecretString = connection.IsEncryptionNegotationComplete ? connection.KeyNegotiator.SharedSecretString : null;

                string commonSalt = null;
                if (connection.IsIncomming && connection.IsEncryptionNegotationComplete)
                {
                    commonSalt = route.BindingPreSharedKey;
                }
                else if (connection.IsOutgoing && connection.IsEncryptionNegotationComplete)
                {
                    commonSalt = route.EndpointPreSharedKey;
                }

                SendPacketEnvelope(connection, new PacketEnvelope
                {
                    Label = IntraServiceLables.TunnelNegotationComplete,
                }, sharedSecretString, commonSalt);
            }
            else if (envelope.Label == IntraServiceLables.TunnelNegotationComplete)
            {
                connection.SetTunnelNegotationComplete();
                //Console.WriteLine("--{0} TunnelNegotationComplete", connection.Route.Name);
                //Console.WriteLine("--{0} Shared Secret: {1}", connection.Route.Name, connection.KeyNegotiator.SharedSecretString);
            }
        }

        void EstablishPeerConnection(object connectionObject)
        {
            SocketState accpetedConnection = (SocketState)connectionObject;

            SocketState foreignConnection = null;

            Endpoint foreignConnectionEndpoint = null;
            string stickeySessionKey = route.Name + ":" + route.Endpoints.ConnectionPattern + ":" + ((IPEndPoint)accpetedConnection.Socket.RemoteEndPoint).Address.ToString();

            if (route.UseStickySessions)
            {
                lock (stickySessionCache)
                {
                    if (stickySessionCache.Contains(stickeySessionKey))
                    {
                        var cacheItem = (StickySession)stickySessionCache[stickeySessionKey];

                        foreignConnectionEndpoint = (from o in route.Endpoints.List
                                                     where o.Address == cacheItem.DestinationAddress && o.Port == cacheItem.DestinationPort
                                                     select o).FirstOrDefault();
                    }
                }
            }

            //Attempt to connect to the same address as we did last time.
            if (foreignConnectionEndpoint != null)
            {
                if ((foreignConnection = Connect(foreignConnectionEndpoint.Address, foreignConnectionEndpoint.Port)) != null)
                {
                    //Success.
                }
            }

            //If not using sticky sessions, of if we failed to connect to the previously successful host - then take the connection pattern into account.
            if (foreignConnection == null)
            {
                if (route.Endpoints.ConnectionPattern == ConnectionPattern.FailOver)
                {
                    foreach (var remotePeer in route.Endpoints.List)
                    {
                        if (remotePeer.Enabled)
                        {
                            if ((foreignConnection = Connect(remotePeer.Address, remotePeer.Port)) != null)
                            {
                                foreignConnectionEndpoint = remotePeer;
                                break;
                            }
                        }
                    }
                }
                else if (route.Endpoints.ConnectionPattern == ConnectionPattern.Balanced)
                {
                    throw new NotImplementedException();
                }
                else if (route.Endpoints.ConnectionPattern == ConnectionPattern.RoundRobbin)
                {
                    if (lastRoundRobinIndex >= route.Endpoints.List.Count)
                    {
                        lastRoundRobinIndex = 0;
                    }

                    int startIndex = lastRoundRobinIndex++;

                    if (startIndex >= route.Endpoints.List.Count)
                    {
                        startIndex = 0;
                    }

                    for (int i = startIndex; i < route.Endpoints.List.Count; i++)
                    {
                        var remotePeer = route.Endpoints.List[i];
                        if (remotePeer.Enabled)
                        {
                            if ((foreignConnection = Connect(remotePeer.Address, remotePeer.Port)) != null)
                            {
                                foreignConnectionEndpoint = remotePeer;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (foreignConnection == null)
            {
                CleanupConnection(accpetedConnection);
                return;
            }

            if (route.UseStickySessions)
            {
                lock (stickySessionCache)
                {
                    var stickySession = new StickySession()
                    {
                        DestinationAddress = foreignConnectionEndpoint.Address,
                        DestinationPort = foreignConnectionEndpoint.Port
                    };

                    stickySessionCache.Add(stickeySessionKey, stickySession, new CacheItemPolicy()
                    {
                        SlidingExpiration = new TimeSpan(0, 0, route.StickySessionCacheExpiration)
                    });
                }
            }

            accpetedConnection.Peer = foreignConnection;
            foreignConnection.Peer = accpetedConnection;

            lock (connections)
            {
                connections.Add(accpetedConnection);
                connections.Add(foreignConnection);
            }

            WaitForData(accpetedConnection);
            WaitForData(foreignConnection);
        }

        #endregion

        private void WaitForData(SocketState connection)
        {
            try
            {
                if (OnDataReceivedCallback == null)
                {
                    OnDataReceivedCallback = new AsyncCallback(OnDataReceived);
                }

                if (connection.BytesReceived == connection.Buffer.Length && connection.BytesReceived < route.MaxBufferSize)
                {
                    int largerBufferSize = connection.Buffer.Length + (connection.Buffer.Length / 4);
                    connection.Buffer = new byte[largerBufferSize];
                }

                Stats.BytesReceived += (UInt64)connection.BytesReceived;

                connection.Socket.BeginReceive(connection.Buffer, 0, connection.Buffer.Length, SocketFlags.None, OnDataReceivedCallback, connection);
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "An error occured while waiting on data.",
                    Exception = ex
                });
            }
        }

        private string ApplyHttpHeaderRules(string httpHeader, HttpHeaderType headerType, string HttpRequestVerb, string lineBreak)
        {
            try
            {
                var availableRules = (from o in route.HttpHeaderRules.List
                                      where
                                      (o.HeaderType == headerType || o.HeaderType == HttpHeaderType.Any)
                                      && (o.Verb.ToString().ToUpper() == HttpRequestVerb.ToUpper() || o.Verb == HTTPVerb.Any)
                                      && o.Enabled == true
                                      select o).ToList();

                foreach (var rule in availableRules)
                {
                    if (rule.Action == HttpHeaderAction.Upsert)
                    {
                        httpHeader = HttpUtility.UpsertHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, lineBreak);
                    }
                    else if (rule.Action == HttpHeaderAction.Update)
                    {
                        httpHeader = HttpUtility.UpdateHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, lineBreak);
                    }
                    else if (rule.Action == HttpHeaderAction.Insert)
                    {
                        httpHeader = HttpUtility.InsertHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, lineBreak);
                    }
                    else if (rule.Action == HttpHeaderAction.Delete)
                    {
                        httpHeader = HttpUtility.DeleteHttpHostHeaderValue(httpHeader, rule.Name, lineBreak);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to process HTTP Header rules.",
                    Exception = ex
                });
            }

            return httpHeader;
        }

        private void OnDataReceived(IAsyncResult asyn)
        {
            SocketState connection = null;

            try
            {
                connection = (SocketState)asyn.AsyncState;
                connection.BytesReceived = connection.Socket.EndReceive(asyn);

                if (connection.BytesReceived == 0)
                {
                    CleanupConnection(connection);
                    return;
                }

                string sharedSecretString = connection.IsEncryptionNegotationComplete ? connection.KeyNegotiator.SharedSecretString : null;

                string commonSalt = null;
                if (connection.IsIncomming && connection.IsEncryptionNegotationComplete)
                {
                    commonSalt = route.BindingPreSharedKey;
                }
                else if (connection.IsOutgoing && connection.IsEncryptionNegotationComplete)
                {
                    commonSalt = route.EndpointPreSharedKey;
                }

                if (connection.UsePackets)
                {
                    //Console.WriteLine("--Recv:{0}, Packet: {1}", route.Name, Encoding.UTF8.GetString(connection.Buffer.Take(connection.BytesReceived).ToArray()));

                    List<PacketEnvelope> envelopes = Packetizer.DissasemblePacketData(this, connection, connection.UseCompression, connection.IsEncryptionNegotationComplete, sharedSecretString, commonSalt);
                    foreach (var envelope in envelopes)
                    {
                        if (envelope.Label == null)
                        {
                            if (connection.IsTunnelNegotationComplete)
                            {
                                //Console.WriteLine("--Recv: {0} {1}", envelope.Payload.Length, Encoding.UTF8.GetString(envelope.Payload.Take(envelope.Payload.Length).ToArray()));
                                ProcessReceivedData(connection, envelope.Payload, envelope.Payload.Length);
                            }
                            else
                            {
                                Stats.DroppedPreNegotiatePacket++;
                                //Console.WriteLine("--Dropped packet, tunnel negotation is not yet complete", route.Name);
                            }
                        }
                        else
                        {
                            ProcessPeerCommand(connection, envelope);
                        }
                    }
                }
                else
                {
                    //Console.WriteLine("--Recv:{0}, Raw: {1}", route.Name, Encoding.UTF8.GetString(connection.Buffer.Take(connection.BytesReceived).ToArray()));

                    //If we are supposed to use encryption, nut its not been initialized then we need to wait before processing the received data.
                    //This is because we do not yet have the information required to decrypt it.
                    //Try a spin lock first, then start sleeping.
                    if (connection.IsTunnelNegotationComplete == false)
                    {
                        WaitForData(connection);

                        int spinCount = route.SpinLockCount;
                        DateTime? startTime = null;

                        while (connection.IsTunnelNegotationComplete == false)
                        {
                            //TODO: This needs to timeout.
                            if (spinCount == 0)
                            {
                                startTime = DateTime.Now;
                            }
                            else if (startTime != null)
                            {
                                if ((DateTime.Now - ((DateTime)startTime)).TotalMilliseconds > route.EncryptionInitilizationTimeoutMS)
                                {
                                    break;
                                }
                                Thread.Sleep(1);
                            }

                            spinCount--;
                        }
                    }

                    if (connection.IsTunnelNegotationComplete)
                    {
                        ProcessReceivedData(connection, connection.Buffer, connection.BytesReceived);
                    }
                    else
                    {
                        Stats.DroppedPreNegotiateRawData++;
                        Console.WriteLine("--Dropped Raw Data Segment", route.Name);
                    }
                }

                WaitForData(connection);
            }
            catch (ObjectDisposedException)
            {
                if (connection != null)
                {
                    CleanupConnection(connection);
                }
                return;
            }
            catch (SocketException)
            {
                if (connection != null)
                {
                    CleanupConnection(connection);
                }
                return;
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to process received data.",
                    Exception = ex
                });
            }
        }

        void ProcessReceivedData(SocketState connection, byte[]buffer, int bufferSize)
        {
            string sharedSecretString = connection.Peer.IsEncryptionNegotationComplete ? connection.Peer.KeyNegotiator.SharedSecretString : null;

            string commonSalt = null;
            if (connection.Peer.IsIncomming && connection.Peer.IsEncryptionNegotationComplete)
            {
                commonSalt = route.BindingPreSharedKey;
            }
            else if (connection.Peer.IsOutgoing && connection.Peer.IsEncryptionNegotationComplete)
            {
                commonSalt = route.EndpointPreSharedKey;
            }

            if (route.TrafficType == TrafficType.HTTP)
            {
                HttpHeaderType httpHeaderType = HttpHeaderType.None;

                string httpHeader = null;
                string httpRequestVerb = string.Empty;

                if (connection.HttpHeaderBuilder != string.Empty)
                {
                    //This is a continuation of a previously received fragmented header.
                    httpHeader = (connection.HttpHeaderBuilder ?? string.Empty) + Encoding.UTF8.GetString(buffer);
                    httpHeaderType = HttpUtility.IsHttpHeader(httpHeader, out httpRequestVerb);
                }
                else
                {
                    httpHeaderType = HttpUtility.IsHttpHeader(buffer, bufferSize, out httpRequestVerb);

                    if (httpHeaderType != HttpHeaderType.None)
                    {
                        httpHeader = Encoding.UTF8.GetString(buffer);
                    }
                }

                if (httpHeaderType != HttpHeaderType.None)
                {
                    string lineBreak = "";
                    int endOfHeaderPos = HttpUtility.GetHttpHeaderEnd(httpHeader, out lineBreak);

                    if (endOfHeaderPos < 0)
                    {
                        throw new NotSupportedException(); //Not sure what to do yet.
                    }

                    if (endOfHeaderPos > bufferSize)
                    {
                        throw new InvalidOperationException(); //Not sure what to do yet.
                    }

                    httpHeader = httpHeader.Substring(0, endOfHeaderPos).Trim(new char[] { '\r', '\n', ' ', '\0' });
                    httpHeader = ApplyHttpHeaderRules(httpHeader, httpHeaderType, httpRequestVerb, lineBreak);
                    httpHeader += lineBreak + lineBreak; //Terminate the header.

                    int contentLength = bufferSize - (endOfHeaderPos - connection.HttpHeaderBuilder.Length);
                    connection.HttpHeaderBuilder = string.Empty;

                    byte[] fullReponse = new byte[httpHeader.Length + contentLength];

                    Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes(httpHeader), 0, fullReponse, 0, httpHeader.Length);
                    if (contentLength > 0)
                    {
                        Buffer.BlockCopy(buffer, endOfHeaderPos, fullReponse, httpHeader.Length, contentLength);
                    }

                    if (connection.Peer.UsePackets)
                    {
                        //Console.WriteLine("--Send:{0}, Packet: {1}", route.Name, Encoding.UTF8.GetString(fullReponse.Take(fullReponse.Length).ToArray()));

                        byte[] sendBuffer = Packetizer.AssembleMessagePacket(fullReponse, fullReponse.Length, connection.Peer.UseCompression, connection.Peer.UseEncryption, sharedSecretString, commonSalt);
                        Stats.BytesSent += (UInt64)sendBuffer.Length;
                        connection.Peer.Socket.Send(sendBuffer, sendBuffer.Length, SocketFlags.None);
                        WaitForData(connection);
                    }
                    else
                    {
                        //Console.WriteLine("--Send:{0}, Raw: {1}", route.Name, Encoding.UTF8.GetString(fullReponse.Take(fullReponse.Length).ToArray()));

                        Stats.BytesSent += (UInt64)fullReponse.Length;
                        connection.Peer.Socket.Send(fullReponse, fullReponse.Length, SocketFlags.None);
                    }

                    return;
                }
            }

            if(connection.Peer.UsePackets)
            {
                //Console.WriteLine("--Send:{0}, Packet: {1}", route.Name, Encoding.UTF8.GetString(buffer.Take(bufferSize).ToArray()));

                byte[] sendBuffer = Packetizer.AssembleMessagePacket(buffer, bufferSize, connection.Peer.UseCompression, connection.Peer.UseEncryption, sharedSecretString, commonSalt);
                Stats.BytesSent += (UInt64)sendBuffer.Length;
                connection.Peer.Socket.Send(sendBuffer, sendBuffer.Length, SocketFlags.None);
            }
            else
            {
                //Console.WriteLine("--Send:{0}, Raw: {1}", route.Name, Encoding.UTF8.GetString(buffer.Take(bufferSize).ToArray()));

                Stats.BytesSent += (UInt64)bufferSize;
                connection.Peer.Socket.Send(buffer, bufferSize, SocketFlags.None);
            }
        }

        private void SendPacketEnvelope(SocketState connection, PacketEnvelope envelope, string encryptionKey, string salt)
        {
            byte[] sendBuffer = Packetizer.AssembleMessagePacket(envelope, connection.UseCompression, true, encryptionKey, salt);
            Stats.BytesSent += (UInt64)sendBuffer.Length;
            connection.Socket.Send(sendBuffer, sendBuffer.Length, SocketFlags.None);
        }
        private void SendPacketEnvelope(SocketState connection, PacketEnvelope envelope)
        {
            byte[] sendBuffer = Packetizer.AssembleMessagePacket(envelope, connection.UseCompression, false, null, null);
            Stats.BytesSent += (UInt64)sendBuffer.Length;
            connection.Socket.Send(sendBuffer, sendBuffer.Length, SocketFlags.None);
        }

        private void CleanupConnection(SocketState connection)
        {
            try
            {
                CleanupSocket(connection.Socket);

                lock (connections)
                {
                    connections.Remove(connection);
                }

                if (connection.Peer != null)
                {
                    CleanupSocket(connection.Peer.Socket);

                    lock (connections)
                    {
                        connections.Remove(connection);
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to clean up connection.",
                    Exception = ex
                });
            }
        }

        private void CleanupSocket(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            try
            {
                socket.Disconnect(false);
            }
            catch
            {
            }
            try
            {
                socket.Close();
            }
            catch
            {
            }
        }

        #region Utility.
        
        public static IPAddress GetIPAddress(string hostName)
        {
            try
            {
                string IP4Address = String.Empty;

                foreach (IPAddress ipAddress in Dns.GetHostAddresses(hostName))
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork
                        || ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        return ipAddress;
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to obtain IP address.",
                    Exception = ex
                });
            }

            return null;
        }

        #endregion
    }
}
