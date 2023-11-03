﻿using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetProxy.Service.Routing
{
    public class Router
    {
        #region Backend Variables.

        public RouterStatistics Stats { get; set; }
        private readonly MemoryCache _stickySessionCache = new(new MemoryCacheOptions());
        private int _lastRoundRobinIndex = 0;
        private readonly Route _route;
        private Socket? _listenSocket = null;
        private readonly List<SocketState> _connections = new();
        private AsyncCallback? _onDataReceivedCallback;

        public int CurrentConnectionCount
        {
            get
            {
                lock (_connections)
                {
                    return _connections.Count;
                }
            }
        }

        public Route Route
        {
            get
            {
                return _route;
            }
        }

        #endregion

        public Router(Route route)
        {
            Stats = new RouterStatistics();
            _route = route;
        }

        public bool IsRunning
        {
            get
            {
                return _listenSocket != null;
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

                if (_route.BindingProtocal == BindingProtocal.Pv6)
                {
                    _listenSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                }
                else if (_route.BindingProtocal == BindingProtocal.Pv4)
                {
                    _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (_route.ListenOnAllAddresses)
                {
                    if (_route.BindingProtocal == BindingProtocal.Pv6)
                    {
                        IPEndPoint ipLocal = new IPEndPoint(IPAddress.IPv6Any, _route.ListenPort);
                        _listenSocket.Bind(ipLocal);
                    }
                    else if (_route.BindingProtocal == BindingProtocal.Pv4)
                    {
                        IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, _route.ListenPort);
                        _listenSocket.Bind(ipLocal);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    foreach (var binding in _route.Bindings)
                    {
                        if (binding.Enabled)
                        {
                            IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(binding.Address), _route.ListenPort);
                            _listenSocket.Bind(ipLocal);
                        }
                    }
                }

                _listenSocket.Listen(_route.AcceptBacklogSize);
                _listenSocket.BeginAccept(new AsyncCallback(OnConnectionAccepted), null);

                return true;
            }
            catch (Exception ex)
            {
                if (_listenSocket != null)
                {
                    try
                    {
                        _listenSocket.Close();
                    }
                    catch
                    {
                    }
                }
                _listenSocket = null;

                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
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
                if (_listenSocket != null)
                {
                    _listenSocket.Close();
                    _listenSocket = null;
                }

                lock (_connections)
                {
                    while (_connections.Count > 0)
                    {
                        try
                        {
                            CleanupConnection(_connections[0]);
                        }
                        catch
                        {
                        }
                    }

                    _connections.Clear();
                }
            }
            catch (Exception ex)
            {
                _listenSocket = null;

                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
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
                if (_listenSocket != null)
                {
                    Stats.TotalConnections++;

                    Socket socket = _listenSocket.EndAccept(asyn);
                    SocketState connection = new SocketState(socket, _route.InitialBufferSize);

                    connection.Route = _route;
                    connection.IsIncomming = true;

                    (new Thread(EstablishPeerConnection)).Start(connection);

                    _listenSocket.BeginAccept(new AsyncCallback(OnConnectionAccepted), null);
                }
            }
            catch
            {
                //Discard.
            }
        }

        public SocketState? Connect(string hostName, int port)
        {
            IPAddress? ipAddress = GetIpAddress(hostName);
            if (ipAddress != null)
            {
                return Connect(ipAddress, port);
            }
            return null;
        }

        public SocketState? Connect(IPAddress ipAddress, int port)
        {
            try
            {
                Socket socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipEnd = new IPEndPoint(ipAddress, port);

                socket.Connect(ipEnd);
                if (socket.Connected)
                {
                    Stats.TotalConnections++;
                    var connection = new SocketState(socket, _route.InitialBufferSize);

                    connection.IsOutgoing = true;
                    connection.Route = _route;

                    return connection;
                }
            }
            catch
            {
            }

            return null;
        }

        void EstablishPeerConnection(object? connectionObject)
        {
            var accpetedConnection = connectionObject as SocketState;
            Utility.EnsureNotNull(accpetedConnection);

            Utility.EnsureNotNull(accpetedConnection.Socket);
            Utility.EnsureNotNull(accpetedConnection.Socket.RemoteEndPoint);

            SocketState? foreignConnection = null;

            Endpoint? foreignConnectionEndpoint = null;
            string stickeySessionKey = _route.Name + ":" + _route.Endpoints.ConnectionPattern
                + ":" + ((IPEndPoint)accpetedConnection.Socket.RemoteEndPoint).Address?.ToString();

            if (_route.UseStickySessions)
            {
                lock (_stickySessionCache)
                {
                    if (_stickySessionCache.TryGetValue(stickeySessionKey, out StickySession? cacheItem) && cacheItem != null)
                    {
                        foreignConnectionEndpoint = (from o in _route.Endpoints.List
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
                if (_route.Endpoints.ConnectionPattern == ConnectionPattern.FailOver)
                {
                    foreach (var remotePeer in _route.Endpoints.List)
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
                else if (_route.Endpoints.ConnectionPattern == ConnectionPattern.Balanced)
                {
                    throw new NotImplementedException();
                }
                else if (_route.Endpoints.ConnectionPattern == ConnectionPattern.RoundRobbin)
                {
                    if (_lastRoundRobinIndex >= _route.Endpoints.List.Count)
                    {
                        _lastRoundRobinIndex = 0;
                    }

                    int startIndex = _lastRoundRobinIndex++;

                    if (startIndex >= _route.Endpoints.List.Count)
                    {
                        startIndex = 0;
                    }

                    for (int i = startIndex; i < _route.Endpoints.List.Count; i++)
                    {
                        var remotePeer = _route.Endpoints.List[i];
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

            if (_route.UseStickySessions)
            {
                Utility.EnsureNotNull(foreignConnectionEndpoint);

                lock (_stickySessionCache)
                {
                    var stickySession = new StickySession()
                    {
                        DestinationAddress = foreignConnectionEndpoint.Address,
                        DestinationPort = foreignConnectionEndpoint.Port
                    };

                    _stickySessionCache.Set(stickeySessionKey, stickySession, DateTime.Now.AddSeconds(_route.StickySessionCacheExpiration));
                }
            }

            accpetedConnection.Peer = foreignConnection;
            foreignConnection.Peer = accpetedConnection;

            lock (_connections)
            {
                _connections.Add(accpetedConnection);
                _connections.Add(foreignConnection);
            }

            WaitForData(accpetedConnection);
            WaitForData(foreignConnection);
        }

        #endregion

        private void WaitForData(SocketState connection)
        {
            try
            {
                if (_onDataReceivedCallback == null)
                {
                    _onDataReceivedCallback = new AsyncCallback(OnDataReceived);
                }

                if (connection.BytesReceived == connection.Buffer.Length && connection.BytesReceived < _route.MaxBufferSize)
                {
                    int largerBufferSize = connection.Buffer.Length + (connection.Buffer.Length / 4);
                    connection.Buffer = new byte[largerBufferSize];
                }

                Stats.BytesReceived += (ulong)connection.BytesReceived;

                Utility.EnsureNotNull(connection?.Socket);
                connection.Socket.BeginReceive(connection.Buffer, 0, connection.Buffer.Length, SocketFlags.None, _onDataReceivedCallback, connection);
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "An error occured while waiting on data.",
                    Exception = ex
                });
            }
        }

        private string ApplyHttpHeaderRules(string httpHeader, HttpHeaderType headerType, string httpRequestVerb, string lineBreak)
        {
            try
            {
                var availableRules = (from o in _route.HttpHeaderRules.List
                                      where
                                      (o.HeaderType == headerType || o.HeaderType == HttpHeaderType.Any)
                                      && (o.Verb.ToString().ToUpper() == httpRequestVerb.ToUpper() || o.Verb == HttpVerb.Any)
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
                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to process HTTP Header rules.",
                    Exception = ex
                });
            }

            return httpHeader;
        }

        private void OnDataReceived(IAsyncResult asyn)
        {
            SocketState? connection = null;

            try
            {
                connection = asyn.AsyncState as SocketState;
                Utility.EnsureNotNull(connection?.Socket);

                connection.BytesReceived = connection.Socket.EndReceive(asyn);

                if (connection.BytesReceived == 0)
                {
                    CleanupConnection(connection);
                    return;
                }

                ProcessReceivedData(connection, connection.Buffer, connection.BytesReceived);

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
                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to process received data.",
                    Exception = ex
                });
            }
        }

        void ProcessReceivedData(SocketState connection, byte[]? buffer, int bufferSize)
        {
            if (buffer == null)
            {
                buffer = Array.Empty<byte>();
            }

            Utility.EnsureNotNull(connection.Peer);

            if (_route.TrafficType == TrafficType.Http)
            {
                HttpHeaderType httpHeaderType = HttpHeaderType.None;

                string? httpHeader = null;
                string? httpRequestVerb = string.Empty;

                if (connection != null && connection.HttpHeaderBuilder != string.Empty)
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

                if (httpHeaderType != HttpHeaderType.None && httpHeader != null)
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


                    Utility.EnsureNotNull(httpRequestVerb);
                    Utility.EnsureNotNull(connection?.HttpHeaderBuilder);

                    httpHeader = httpHeader.Substring(0, endOfHeaderPos).Trim(new char[] { '\r', '\n', ' ', '\0' });
                    httpHeader = ApplyHttpHeaderRules(httpHeader, httpHeaderType, httpRequestVerb, lineBreak);
                    httpHeader += lineBreak + lineBreak; //Terminate the header.

                    int contentLength = bufferSize - (endOfHeaderPos - connection.HttpHeaderBuilder.Length);
                    connection.HttpHeaderBuilder = string.Empty;

                    byte[] fullReponse = new byte[httpHeader.Length + contentLength];

                    Buffer.BlockCopy(Encoding.UTF8.GetBytes(httpHeader), 0, fullReponse, 0, httpHeader.Length);
                    if (contentLength > 0)
                    {
                        Buffer.BlockCopy(buffer, endOfHeaderPos, fullReponse, httpHeader.Length, contentLength);
                    }

                    //Console.WriteLine("--Send:{0}, Raw: {1}", route.Name, Encoding.UTF8.GetString(fullReponse.Take(fullReponse.Length).ToArray()));

                    Stats.BytesSent += (ulong)fullReponse.Length;

                    Utility.EnsureNotNull(connection?.Peer?.Socket);
                    connection.Peer.Socket.Send(fullReponse, fullReponse.Length, SocketFlags.None);

                    return;
                }
            }

            //Console.WriteLine("--Send:{0}, Raw: {1}", route.Name, Encoding.UTF8.GetString(buffer.Take(bufferSize).ToArray()));

            Stats.BytesSent += (ulong)bufferSize;

            Utility.EnsureNotNull(connection?.Peer?.Socket);
            connection.Peer.Socket.Send(buffer, bufferSize, SocketFlags.None);
        }

        private void CleanupConnection(SocketState? connection)
        {
            try
            {
                CleanupSocket(connection?.Socket);

                if (connection != null)
                {
                    lock (_connections)
                    {
                        _connections.Remove(connection);
                    }
                }

                if (connection?.Peer != null)
                {
                    CleanupSocket(connection.Peer.Socket);

                    lock (_connections)
                    {
                        _connections.Remove(connection);
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to clean up connection.",
                    Exception = ex
                });
            }
        }

        private void CleanupSocket(Socket? socket)
        {
            try { socket?.Shutdown(SocketShutdown.Both); } catch { }
            try { socket?.Disconnect(false); } catch { }
            try { socket?.Close(); } catch { }
        }

        #region Utility.

        public static IPAddress? GetIpAddress(string hostName)
        {
            try
            {
                string ip4Address = string.Empty;

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
                Singletons.EventLog.WriteLog(new Logging.LoggingPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to obtain IP address.",
                    Exception = ex
                });
            }

            return null;
        }

        #endregion
    }
}
