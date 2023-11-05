using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace NetProxy.Service.Proxy
{
    internal class NpProxyConnection
    {
        public ConnectionDirection Direction { get; private set; }

        private readonly TcpClient _tcpclient; //The TCP/IP connection associated with this connection.
        private readonly Thread _dataPumpThread; //The thread that receives data for this connection.
        private readonly NetworkStream _stream; //The stream for the TCP/IP connection (used for reading and writing).
        private readonly NpProxyListener _listener; //The listener which owns this connection.
        private NpProxyConnection? _peer; //The associated endpoint connection for this connection.
        private bool _keepRunning;
        private NpEndpoint? _outboundEndpoint; //For outbound connections, this is the endpoint that was used to make this connection.

        public Guid Id { get; private set; }
        public DateTime StartDateTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime { get; private set; } = DateTime.UtcNow;

        public NpProxyConnection(NpProxyListener listener, TcpClient tcpClient)
        {
            Id = Guid.NewGuid();
            _tcpclient = tcpClient;
            _dataPumpThread = new Thread(DataPumpThreadProc);
            _keepRunning = true;
            _listener = listener;
            _stream = tcpClient.GetStream();
        }

        public void Write(byte[] buffer)
        {
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) => o[_outboundEndpoint.Id].BytesWritten += (ulong)buffer.Length);
            }

            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer);
        }

        public void Write(byte[] buffer, int length)
        {
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) => o[_outboundEndpoint.Id].BytesWritten += (ulong)length);
            }

            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer, 0, length);
        }

        public bool Read(ref byte[] buffer, out int outLength)
        {
            LastActivityDateTime = DateTime.UtcNow;
            int length = _stream.Read(buffer, 0, buffer.Length);

            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) => o[_outboundEndpoint.Id].BytesRead += (ulong)length);
            }

            outLength = length;

            return length > 0;
        }

        /// <summary>
        /// We received an inbound connection which is now open and ready. This is where we establish the connection to the associated endpoint.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void RunInboundAsync()
        {
            Direction = ConnectionDirection.Inbound;

            HashSet<Guid> triedEndpoints = new();

            int lastTriedEndpointIndex = _listener.LastTriedEndpointIndex;

            NpEndpoint? endpoint = null;
            var endpoints = _listener.Proxy.Route.Endpoints.Collection;
            if (endpoints.Count == 0)
            {
                throw new Exception("The route has no defined endpoints.");
            }

            var remoteEndPoint = _tcpclient.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint == null)
            {
                throw new Exception("Could not determine remote endpoint from the client.");
            }

            var sessionKey = $"{_listener.Proxy.Route.Name}:{_listener.Proxy.Route.Endpoints.ConnectionPattern}:{remoteEndPoint.Address}";

            TcpClient? establishedConnection = null;

            #region First try sticky sessions....
            if (_listener.Proxy.Route.UseStickySessions)
            {
                if (_listener.StickySessionCache.TryGetValue(sessionKey, out NpStickySession? cacheItem) && cacheItem != null)
                {
                    endpoint = endpoints.Where(o => o.Address == cacheItem.Address && o.Port == cacheItem.Port).FirstOrDefault();
                }

                if (endpoint != null)
                {
                    triedEndpoints.Add(endpoint.Id);
                    try
                    {
                        //Make the outbound connection to the endpoint specified for this route (and sticky session).
                        establishedConnection = new TcpClient(endpoint.Address, endpoint.Port);
                    }
                    catch
                    {
                        NpUtility.TryAndIgnore(() => establishedConnection?.Close());
                        establishedConnection = null;
                    }
                }
            }
            #endregion

            //If and while we do not have a connection, lets determine what endpoint we should use.
            while (establishedConnection == null)
            {
                if (endpoints.Where(o => triedEndpoints.Contains(o.Id) == false).Any() == false)
                {
                    throw new Exception("All endpoints were exhausted while trying to connect to the remote peer.");
                }

                if (_listener.Proxy.Route.Endpoints.ConnectionPattern == ConnectionPattern.RoundRobbin)
                {
                    lastTriedEndpointIndex++;

                    if (lastTriedEndpointIndex >= endpoints.Count)
                    {
                        //We mayhave started trying RoundRobbin connections at a non-zero index, if we reached the end, start back at zero.
                        //We use "triedEndpoints" to determine when we have tired them all.
                        lastTriedEndpointIndex = 0;
                    }

                    endpoint = endpoints[lastTriedEndpointIndex];
                }
                else if (_listener.Proxy.Route.Endpoints.ConnectionPattern == ConnectionPattern.Balanced)
                {
                    //Get the id of the endpoint with the least connections.
                    var endpointId = _listener.EndpointStatistics.Use((o) =>
                    {
                        return o.OrderBy(e => e.Value.CurrentConnections).First().Key;
                    });

                    endpoint = endpoints.Where(o => o.Id == endpointId).First();
                }
                else if (_listener.Proxy.Route.Endpoints.ConnectionPattern == ConnectionPattern.FailOver)
                {
                    //Starting at the last tried endpoint index, look for endpoints that we have not tired.
                    while (triedEndpoints.Contains(endpoints[lastTriedEndpointIndex].Id))
                    {
                        if (lastTriedEndpointIndex >= endpoints.Count)
                        {
                            lastTriedEndpointIndex = 0;
                        }
                        else
                        {
                            lastTriedEndpointIndex++;
                        }
                    }

                    endpoint = endpoints[lastTriedEndpointIndex];
                }
                else
                {
                    throw new Exception($"The connection pattern {_listener.Proxy.Route.Endpoints.ConnectionPattern} is not implemented.");
                }

                try
                {
                    //Make the outbound connection to the endpoint specified for this route.
                    triedEndpoints.Add(endpoint.Id);
                    establishedConnection = new TcpClient(endpoint.Address, endpoint.Port);
                }
                catch
                {
                    NpUtility.TryAndIgnore(() => establishedConnection?.Close());
                    establishedConnection = null;
                }
            }

            if (endpoint == null)
            {
                throw new Exception($"A connection was estanblished but the endpoint remains undefined.");
            }

            _listener.LastTriedEndpointIndex = lastTriedEndpointIndex; //Make sure other connections can start looking for endpoints where we left off.

            if (_listener.Proxy.Route.UseStickySessions)
            {
                //Keep track of the successful stucky session.
                _listener.StickySessionCache.Set(sessionKey, new NpStickySession(endpoint.Address, endpoint.Port));
            }

            _peer = new NpProxyConnection(_listener, establishedConnection);
            _peer.RunOutboundAsync(this, endpoint);

            //If we were successful making the outbound connection, then start the inbound connection thread.
            _dataPumpThread.Start();
        }

        public void RunOutboundAsync(NpProxyConnection peer, NpEndpoint endpoint)
        {
            Direction = ConnectionDirection.Outbound;

            _outboundEndpoint = endpoint;
            _peer = peer; //Each active connection needs a reference to the opposite endpoint connection.
            _dataPumpThread.Start();
        }

        internal void DataPumpThreadProc()
        {
            Thread.CurrentThread.Name = $"DataPumpThreadProc:{Thread.CurrentThread.ManagedThreadId}";

            #region Track endpoint statistics.
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) =>
                {
                    var stat = o[_outboundEndpoint.Id];
                    stat.CurrentConnections++;
                    stat.TotalConnections++;
                });
            }
            #endregion

            try
            {

                //byte[] buffer = new byte[_listener.Proxy.Route.InitialBufferSize];

                var buffer = new byte[64];

                StringBuilder? headerBuilder = null;

                while (_keepRunning && Read(ref buffer, out int length))
                {
                    if (Direction == ConnectionDirection.Inbound)
                    {
                        if (headerBuilder != null) //We are reconstructing a fragmented HTTP request header.
                        {
                            var stringContent = Encoding.UTF8.GetString(buffer);
                            headerBuilder.Append(stringContent);
                        }
                        else if (HttpUtility.StartsWithHTTPVerb(buffer))
                        {
                            var stringContent = Encoding.UTF8.GetString(buffer);
                            headerBuilder = new StringBuilder(stringContent);
                        }

                        if (headerBuilder != null)
                        {
                            var headerType = HttpUtility.IsHttpHeader(headerBuilder.ToString(), out string? verb);

                            if (headerType != HttpHeaderType.None)
                            {
                                var end = HttpUtility.GetHttpHeaderEnd(headerBuilder.ToString(), out string delimiter);
                                if (end < 0)
                                {
                                    //We have a HTTP header but its a fragment. Wait on the remaining header.
                                    continue;
                                }
                                else
                                {
                                }
                            }
                        }

                    }


                    _peer?.Write(buffer, length);
                }
            }
            catch
            {
            }
            finally
            {
                NpUtility.TryAndIgnore(() => _peer?.Stop(false)); //Tell the peer connection to disconnect.

                #region Track endpoint statistics.
                if (_outboundEndpoint != null)
                {
                    _listener.EndpointStatistics.Use((o) =>
                    {
                        var stat = o[_outboundEndpoint.Id];
                        stat.CurrentConnections--;

                    });
                }
                #endregion
            }

            _listener.RemoveActiveConnection(this);
        }

        public void Stop(bool waitOnThread)
        {
            _keepRunning = false;
            try { _stream.Close(); } catch { }
            try { _tcpclient.Close(); } catch { }

            if (waitOnThread)
            {
                _dataPumpThread.Join();
            }
        }
    }
}
