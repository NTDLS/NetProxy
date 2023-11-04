using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    internal class NpRouterConnection
    {
        private readonly TcpClient _tcpclient; //The TCP/IP connection associated with this connection.
        private readonly Thread _dataPumpThread; //The thread that receives data for this connection.
        private readonly NetworkStream _stream; //The stream for the TCP/IP connection (used for reading and writing).
        private readonly NpRouterListener _listener; //The listener which owns this connection.
        private NpRouterConnection? _peer; //The associated endpoint connection for this connection.
        private bool _keepRunning;

        public Guid Id { get; private set; }
        public DateTime StartDateTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime { get; private set; } = DateTime.UtcNow;

        public NpRouterConnection(NpRouterListener listener, TcpClient tcpClient)
        {
            Id = Guid.NewGuid();
            _tcpclient = tcpClient;
            _dataPumpThread = new Thread(DataPumpThread);
            _keepRunning = true;
            _listener = listener;
            _stream = tcpClient.GetStream();
        }

        public void Write(byte[] buffer)
        {
            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer);
        }

        public void Write(byte[] buffer, int length)
        {
            LastActivityDateTime = DateTime.UtcNow;
            _stream.Write(buffer, 0, length);
        }

        public bool Read(ref byte[] buffer, out int length)
        {
            LastActivityDateTime = DateTime.UtcNow;
            length = _stream.Read(buffer, 0, buffer.Length);
            return length > 0;
        }

        /// <summary>
        /// We received an inbound connection which is now open and ready. This is where we establish the connection to the associated endpoint.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void RunInboundAsync()
        {
            HashSet<Guid> triedEndpoints = new();

            NpEndpoint? endpoint = null;
            var endpoints = _listener.Router.Route.Endpoints.Collection;
            if (endpoints.Count == 0)
            {
                throw new Exception("The route has no defined endpoints.");
            }

            TcpClient? establishedConnection = null;

            //First try sticky sessions.
            if (_listener.Router.Route.UseStickySessions)
            {
                var remoteEndPoint = _tcpclient.Client.RemoteEndPoint as IPEndPoint;
                if (remoteEndPoint == null)
                {
                    throw new Exception("Could not determine remote endpoint from the client.");
                }

                var stickeySessionKey = $"{_listener.Router.Route.Name}:{_listener.Router.Route.Endpoints.ConnectionPattern}:{remoteEndPoint.Address}";
                if (_listener.StickySessionCache.TryGetValue(stickeySessionKey, out NpStickySession? cacheItem) && cacheItem != null)
                {
                    endpoint = endpoints.Where(o => o.Address == cacheItem.DestinationAddress && o.Port == cacheItem.DestinationPort).FirstOrDefault();
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

            //If and while we do not have a connection, lets determine what endpoint we should use.
            while (establishedConnection == null)
            {
                if (_listener.Router.Route.Endpoints.ConnectionPattern == Library.ConnectionPattern.RoundRobbin)
                {
                    endpoint = endpoints.First();
                }
                else if (_listener.Router.Route.Endpoints.ConnectionPattern == Library.ConnectionPattern.Balanced)
                {
                    endpoint = endpoints.First();
                }
                else if (_listener.Router.Route.Endpoints.ConnectionPattern == Library.ConnectionPattern.FailOver)
                {
                    endpoint = endpoints.First();
                }
                else
                {
                    throw new Exception($"The connection pattern {_listener.Router.Route.Endpoints.ConnectionPattern} is not implemented.");
                }

                try
                {
                    //Make the outbound connection to the endpoint specified for this route.
                    establishedConnection = new TcpClient(endpoint.Address, endpoint.Port);
                }
                catch
                {
                    NpUtility.TryAndIgnore(() => establishedConnection?.Close());
                    establishedConnection = null;
                }
            }

            _peer = new NpRouterConnection(_listener, establishedConnection);
            _peer.RunOutboundAsync(this);

            //If we were successful making the outbound connection, then start the inbound connection thread.
            _dataPumpThread.Start();
        }

        public void RunOutboundAsync(NpRouterConnection peer)
        {
            _peer = peer; //Each active connection needs a reference to the opposite endpoint connection.
            _dataPumpThread.Start();
        }

        internal void DataPumpThread()
        {
            byte[] buffer = new byte[_listener.Router.Route.InitialBufferSize];

            while (_keepRunning && Read(ref buffer, out int length))
            {
                _peer?.Write(buffer, length);
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
