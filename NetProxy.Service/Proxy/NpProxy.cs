using NetProxy.Library.Routing;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    public class NpProxy
    {
        public NpProxyStatistics Statistics { get; private set; }
        public  NpProxyConfiguration Configuration { get; private set; }
        private readonly List<NpProxyListener> _listeners = new();
        private bool _keepRunning = false;

        public bool IsRunning => _keepRunning;
        public int CurrentConnectionCount => 1000;

        public NpProxy(NpProxyConfiguration configuration)
        {
            Statistics = new NpProxyStatistics();
            Configuration = configuration;
        }

        public bool Start()
        {
            try
            {
                if (_keepRunning)
                {
                    return true;
                }

                _keepRunning = true;

                if (Configuration.ListenOnAllAddresses)
                {
                    var tcpListener = new TcpListener(IPAddress.Any, Configuration.ListenPort);
                    var listener = new NpProxyListener(this, tcpListener);
                    _listeners.Add(listener);
                }
                else
                {
                    foreach (var binding in Configuration.Bindings.Where(o => o.Enabled == true))
                    {
                        var tcpListener = new TcpListener(IPAddress.Parse(binding.Address), Configuration.ListenPort);
                        var listener = new NpProxyListener(this, tcpListener);
                        _listeners.Add(listener);
                    }
                }

                foreach (var listener in _listeners)
                {
                    listener.StartAsync();
                }

                return true;
            }
            catch
            {
                //TODO: Log this.
            }
            return false;
        }

        public void Stop()
        {
            foreach (var listener in _listeners)
            {
                listener.Stop();
            }
        }

        /*


        void EstablishPeerConnection(object? connectionObject)
        {
            var accpetedConnection = connectionObject as SocketState;
            Utility.EnsureNotNull(accpetedConnection);

            Utility.EnsureNotNull(accpetedConnection.Socket);
            Utility.EnsureNotNull(accpetedConnection.Socket.RemoteEndPoint);

            SocketState? foreignConnection = null;

            Endpoint? foreignConnectionEndpoint = null;
            string stickeySessionKey = _proxy.Name + ":" + _proxy.Endpoints.ConnectionPattern
                + ":" + ((IPEndPoint)accpetedConnection.Socket.RemoteEndPoint).Address?.ToString();

            if (_proxy.UseStickySessions)
            {
                lock (_stickySessionCache)
                {
                    if (_stickySessionCache.TryGetValue(stickeySessionKey, out StickySession? cacheItem) && cacheItem != null)
                    {
                        foreignConnectionEndpoint = (from o in _proxy.Endpoints.List
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
                if (_proxy.Endpoints.ConnectionPattern == ConnectionPattern.FailOver)
                {
                    foreach (var remotePeer in _proxy.Endpoints.List)
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
                else if (_proxy.Endpoints.ConnectionPattern == ConnectionPattern.Balanced)
                {
                    throw new NotImplementedException();
                }
                else if (_proxy.Endpoints.ConnectionPattern == ConnectionPattern.RoundRobbin)
                {
                    if (_lastRoundRobinIndex >= _proxy.Endpoints.List.Count)
                    {
                        _lastRoundRobinIndex = 0;
                    }

                    int startIndex = _lastRoundRobinIndex++;

                    if (startIndex >= _proxy.Endpoints.List.Count)
                    {
                        startIndex = 0;
                    }

                    for (int i = startIndex; i < _proxy.Endpoints.List.Count; i++)
                    {
                        var remotePeer = _proxy.Endpoints.List[i];
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

            if (_proxy.UseStickySessions)
            {
                Utility.EnsureNotNull(foreignConnectionEndpoint);

                lock (_stickySessionCache)
                {
                    var stickySession = new StickySession()
                    {
                        DestinationAddress = foreignConnectionEndpoint.Address,
                        DestinationPort = foreignConnectionEndpoint.Port
                    };

                    _stickySessionCache.Set(stickeySessionKey, stickySession, DateTime.Now.AddSeconds(_proxy.StickySessionCacheExpiration));
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

                if (connection.BytesReceived == connection.Buffer.Length && connection.BytesReceived < _proxy.MaxBufferSize)
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
                var availableRules = (from o in _proxy.HttpHeaderRules.List
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

            if (_proxy.TrafficType == TrafficType.Http)
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

                    //Console.WriteLine("--Send:{0}, Raw: {1}", proxy.Name, Encoding.UTF8.GetString(fullReponse.Take(fullReponse.Length).ToArray()));

                    Stats.BytesSent += (ulong)fullReponse.Length;

                    Utility.EnsureNotNull(connection?.Peer?.Socket);
                    connection.Peer.Socket.Send(fullReponse, fullReponse.Length, SocketFlags.None);

                    return;
                }
            }

            //Console.WriteLine("--Send:{0}, Raw: {1}", proxy.Name, Encoding.UTF8.GetString(buffer.Take(bufferSize).ToArray()));

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
        */
    }
}
