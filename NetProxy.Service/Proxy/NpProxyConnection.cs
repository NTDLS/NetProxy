using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// This is an endpoint, it contains all of the logic to serve both an inbound and an outbound connection.
    /// </summary>
    internal class NpProxyConnection
    {
        public ConnectionDirection Direction { get; private set; }

        private readonly TcpClient _tcpClient; //The TCP/IP connection associated with this connection.
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
            _tcpClient = tcpClient;
            _dataPumpThread = new Thread(DataPumpThreadProc);
            _keepRunning = true;
            _listener = listener;
            _stream = tcpClient.GetStream();
        }

        public void WriteBytesToPeer(byte[] buffer)
        {
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) =>
                {
                    if (o.TryGetValue(_outboundEndpoint.Id, out NpProxyEndpointStatistics? value))
                    {
                        value.BytesWritten += (ulong)buffer.Length;
                    }
                });
            }
            _listener.Proxy.Statistics.Use((o) => o.BytesWritten += (ulong)buffer.Length);

            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer);
        }

        public void WriteBytesToPeer(byte[] buffer, int length)
        {
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) =>
                {
                    if (o.TryGetValue(_outboundEndpoint.Id, out NpProxyEndpointStatistics? value))
                    {
                        value.BytesWritten += (ulong)length;
                    }
                });
            }
            _listener.Proxy.Statistics.Use((o) => o.BytesWritten += (ulong)length);

            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer, 0, length);
        }

        public bool ReadBytesFromPeer(ref PumpBuffer buffer)
        {
            LastActivityDateTime = DateTime.UtcNow;
            int bytesRead = _stream.Read(buffer.Bytes, 0, buffer.Bytes.Length);

            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) =>
                {
                    if (o.ContainsKey(_outboundEndpoint.Id))
                    {
                        o[_outboundEndpoint.Id].BytesRead += (ulong)bytesRead;
                    }
                });
            }
            _listener.Proxy.Statistics.Use((o) => o.BytesRead += (ulong)bytesRead);

            buffer.Length = bytesRead;

            return bytesRead > 0;
        }

        /// <summary>
        /// We received an inbound connection which is now open and ready.
        /// This is where we establish the connection to the associated outbound endpoint.
        /// </summary>
        public void RunInboundAsync()
        {
            Direction = ConnectionDirection.Inbound;

            HashSet<Guid> triedEndpoints = new();

            int lastTriedEndpointIndex = _listener.LastTriedEndpointIndex;

            NpEndpoint? endpoint = null;
            var endpoints = _listener.Proxy.Configuration.Endpoints.Collection;
            if (endpoints.Count == 0)
            {
                throw new Exception("The proxy has no defined endpoints.");
            }

            var remoteEndPoint = _tcpClient.Client.RemoteEndPoint as IPEndPoint;
            if (remoteEndPoint == null)
            {
                throw new Exception("Could not determine remote endpoint from the client.");
            }

            var sessionKey = $"{_listener.Proxy.Configuration.Name}:{_listener.Proxy.Configuration.Endpoints.ConnectionPattern}:{remoteEndPoint.Address}";

            TcpClient? establishedConnection = null;

            #region First try sticky sessions....

            if (_listener.Proxy.Configuration.UseStickySessions)
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
                        //Make the outbound connection to the endpoint specified for this proxy (and sticky session).
                        establishedConnection = new TcpClient(endpoint.Address, endpoint.Port);
                    }
                    catch
                    {
                        Exceptions.Ignore(() => establishedConnection?.Close());
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

                if (_listener.Proxy.Configuration.Endpoints.ConnectionPattern == ConnectionPattern.RoundRobbin)
                {
                    lastTriedEndpointIndex++;

                    if (lastTriedEndpointIndex >= endpoints.Count)
                    {
                        //We may have started trying RoundRobbin connections at a non-zero index, if we reached the end, start back at zero.
                        //We use "triedEndpoints" to determine when we have tired them all.
                        lastTriedEndpointIndex = 0;
                    }

                    endpoint = endpoints[lastTriedEndpointIndex];
                }
                else if (_listener.Proxy.Configuration.Endpoints.ConnectionPattern == ConnectionPattern.Balanced)
                {
                    //Get the id of the endpoint with the least connections.
                    var endpointId = _listener.EndpointStatistics.Use((o) =>
                    {
                        return o.OrderBy(e => e.Value.CurrentConnections).First().Key;
                    });

                    endpoint = endpoints.Where(o => o.Id == endpointId).First();
                }
                else if (_listener.Proxy.Configuration.Endpoints.ConnectionPattern == ConnectionPattern.FailOver)
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
                    throw new Exception($"The connection pattern {_listener.Proxy.Configuration.Endpoints.ConnectionPattern} is not implemented.");
                }

                try
                {
                    //Make the outbound connection to the endpoint specified for this proxy.
                    triedEndpoints.Add(endpoint.Id);
                    establishedConnection = new TcpClient(endpoint.Address, endpoint.Port);
                }
                catch
                {
                    Exceptions.Ignore(() => establishedConnection?.Close());
                    establishedConnection = null;
                }
            }

            if (endpoint == null)
            {
                throw new Exception($"A connection was established but the endpoint remains undefined.");
            }

            _listener.LastTriedEndpointIndex = lastTriedEndpointIndex; //Make sure other connections can start looking for endpoints where we left off.

            if (_listener.Proxy.Configuration.UseStickySessions)
            {
                //Keep track of the successful sticky session.
                _listener.StickySessionCache.Set(sessionKey, new NpStickySession(endpoint.Address, endpoint.Port));
            }

            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Outbound endpoint connection was established: {endpoint.Id}");

            _peer = new NpProxyConnection(_listener, establishedConnection);
            _peer.RunOutboundAsync(this, endpoint);

            //If we were successful making the outbound connection, then start the inbound connection thread.
            _dataPumpThread.Start();
        }

        /// <summary>
        /// Pump data for the outbound endpoint.
        /// </summary>
        public void RunOutboundAsync(NpProxyConnection peer, NpEndpoint endpoint)
        {
            Direction = ConnectionDirection.Outbound;

            _outboundEndpoint = endpoint;
            _peer = peer; //Each active connection needs a reference to the opposite endpoint connection.
            _dataPumpThread.Start();
        }

        internal void DataPumpThreadProc()
        {
            Thread.CurrentThread.Name = $"DataPumpThreadProc:{Environment.CurrentManagedThreadId}";

            #region Track endpoint statistics.
            if (_outboundEndpoint != null)
            {
                _listener.EndpointStatistics.Use((o) =>
                {
                    if (o.TryGetValue(_outboundEndpoint.Id, out NpProxyEndpointStatistics? stat))
                    {
                        stat.CurrentConnections++;
                        stat.TotalConnections++;
                    }
                });
            }
            #endregion

            try
            {
                var buffer = new PumpBuffer(_listener.Proxy.Configuration.InitialBufferSize);

                var httpHeaderBuilder = new StringBuilder();

                while (_keepRunning && ReadBytesFromPeer(ref buffer))
                {
                    #region HTTP Header Augmentation.

                    if (
                        //Only parse HTTP headers if the traffic type is HTTP.
                        _listener.Proxy.Configuration.TrafficType == TrafficType.Http
                        &&
                        (
                            // and the direction is inbound and we have request rules.
                            (
                                Direction == ConnectionDirection.Inbound
                                && _listener.Proxy.Configuration.HttpHeaderRules.Collection
                                    .Where(o => (new[] { HttpHeaderType.Request, HttpHeaderType.Any }).Contains(o.HeaderType)).Any()
                            )
                            // or the direction is outbound and we have response rules.
                            || (
                                Direction == ConnectionDirection.Outbound
                                && _listener.Proxy.Configuration.HttpHeaderRules.Collection
                                    .Where(o => (new[] { HttpHeaderType.Response, HttpHeaderType.Any }).Contains(o.HeaderType)).Any()
                            )
                        )
                    )
                    {
                        switch (HttpHeaderAugmentation.Process(ref httpHeaderBuilder, _listener.Proxy.Configuration, buffer))
                        {
                            case HttpHeaderAugmentation.HTTPHeaderResult.WaitOnData:
                                //We received a partial HTTP header, wait on more data.
                                continue;
                            case HttpHeaderAugmentation.HTTPHeaderResult.Present:
                                //Found an HTTP header, send it to the peer. There may be bytes remaining
                                //  in the buffer if buffer.Length > 0 so follow though to WriteBytesToPeer.
                                _peer?.WriteBytesToPeer(Encoding.UTF8.GetBytes(httpHeaderBuilder.ToString()));
                                httpHeaderBuilder.Clear();
                                break;
                            case HttpHeaderAugmentation.HTTPHeaderResult.NotPresent:
                                //Didn't find an HTTP header.
                                break;
                        }
                    }

                    #endregion

                    if (buffer.Length > 0)
                    {
                        _peer?.WriteBytesToPeer(buffer.Bytes, buffer.Length); //Send data to remote peer.
                    }

                    buffer.AutoResize(_listener.Proxy.Configuration.MaxBufferSize);
                }
            }
            catch
            {
            }
            finally
            {
                Exceptions.Ignore(() => _peer?.Stop(false)); //Tell the peer connection to disconnect.

                #region Track endpoint statistics.
                if (_outboundEndpoint != null)
                {
                    _listener.EndpointStatistics.Use((o) =>
                    {
                        if (o.ContainsKey(_outboundEndpoint.Id))
                        {
                            var stat = o[_outboundEndpoint.Id];
                            stat.CurrentConnections--;
                        }
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
            try { _stream.Dispose(); } catch { }
            try { _tcpClient.Close(); } catch { }
            try { _tcpClient.Dispose(); } catch { }

            if (waitOnThread)
            {
                _dataPumpThread.Join();
            }
        }
    }
}
