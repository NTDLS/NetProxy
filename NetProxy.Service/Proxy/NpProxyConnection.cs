using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
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

        public bool ReadBytesFromPeer(ref byte[] buffer, out int outBytesRead)
        {
            LastActivityDateTime = DateTime.UtcNow;
            int bytesRead = _stream.Read(buffer, 0, buffer.Length);

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

            outBytesRead = bytesRead;

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
                    NpUtility.TryAndIgnore(() => establishedConnection?.Close());
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
                var buffer = new byte[_listener.Proxy.Configuration.InitialBufferSize];

                StringBuilder? httpRequestHeaderBuilder = null;

                while (_keepRunning && ReadBytesFromPeer(ref buffer, out int bufferLength))
                {
                    #region HTTP Header augmentation.

                    try
                    {
                        if (
                            //Only parse HTTP headers if the traffic type is HTTP
                            _listener.Proxy.Configuration.TrafficType == TrafficType.Http
                            &&
                            (
                                // and the direction is inbound and we have request rules
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
                            if (httpRequestHeaderBuilder != null) //We are reconstructing a fragmented HTTP request header.
                            {
                                var stringContent = Encoding.UTF8.GetString(buffer, 0, bufferLength);
                                httpRequestHeaderBuilder.Append(stringContent);
                            }
                            else if (HttpUtility.StartsWithHTTPVerb(buffer))
                            {
                                var stringContent = Encoding.UTF8.GetString(buffer, 0, bufferLength);
                                httpRequestHeaderBuilder = new StringBuilder(stringContent);
                            }

                            string headerDelimiter = string.Empty;

                            if (httpRequestHeaderBuilder != null)
                            {
                                var headerType = HttpUtility.IsHttpHeader(httpRequestHeaderBuilder.ToString(), out string? requestVerb);

                                if (headerType != HttpHeaderType.None && requestVerb != null)
                                {
                                    var endOfHeaderIndex = HttpUtility.GetHttpHeaderEnd(httpRequestHeaderBuilder.ToString(), out headerDelimiter);
                                    if (endOfHeaderIndex < 0)
                                    {
                                        continue; //We have a HTTP header but its a fragment. Wait on the remaining header.
                                    }
                                    else
                                    {
                                        if (bufferLength > headerDelimiter.Length * 2) // "\r\n" or "\r\n\r\n"
                                        {
                                            //If we received more bytes than just the delimiter then we
                                            //  need to determine how many non-header bytes need to be sent to the peer.

                                            int endOfHeaderInBufferIndex = HttpUtility.FindDelimiterIndexInByteArray(buffer, bufferLength, $"{headerDelimiter}{headerDelimiter}");
                                            if (endOfHeaderInBufferIndex < 0)
                                            {
                                                throw new Exception("Could not locate HTTP header in receive buffer.");
                                            }

                                            int bufferEndOfHeaderOffset = endOfHeaderInBufferIndex + (headerDelimiter.Length * 2);

                                            if (bufferEndOfHeaderOffset > bufferLength)
                                            {
                                                //We received extra non-header bytes. We need to remove the header bytes from the buffer
                                                //  and then send them after we modify and send the header.
                                                int newBufferLength = bufferLength - bufferEndOfHeaderOffset;
                                                Array.Copy(buffer, bufferEndOfHeaderOffset, buffer, 0, newBufferLength);
                                            }

                                            bufferLength -= bufferEndOfHeaderOffset;
                                        }
                                        else
                                        {
                                            bufferLength = 0;
                                        }
                                    }

                                    //apply the header rules:
                                    string modifiedHttpRequestHeader = HttpUtility.ApplyHttpHeaderRules
                                        (_listener.Proxy.Configuration, httpRequestHeaderBuilder.ToString(), headerType, requestVerb, headerDelimiter);
                                    httpRequestHeaderBuilder = null; //We have completed reconstructing the header and performed modifications.

                                    //Send the modified header to the peer.
                                    _peer?.WriteBytesToPeer(Encoding.UTF8.GetBytes(modifiedHttpRequestHeader));

                                    if (bufferLength == 0)
                                    {
                                        //All we received is the header, so that;s all we have to send at this time.
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        httpRequestHeaderBuilder = null;
                        Singletons.Logging.Write("An error occurred while parsing the HTTP request header.", ex);
                    }

                    #endregion

                    _peer?.WriteBytesToPeer(buffer, bufferLength); //Send data to remote peer.

                    #region Buffer resize.
                    if (bufferLength == buffer.Length && buffer.Length < _listener.Proxy.Configuration.MaxBufferSize)
                    {
                        //If we read as much data as we could fit in the buffer, resize it a bit up to the maximum.
                        int newBufferSize = (int)(buffer.Length + (buffer.Length * 0.20));
                        if (newBufferSize > _listener.Proxy.Configuration.MaxBufferSize)
                        {
                            newBufferSize = _listener.Proxy.Configuration.MaxBufferSize;
                        }

                        buffer = new byte[newBufferSize];
                    }
                    #endregion
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
