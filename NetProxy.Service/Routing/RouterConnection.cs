using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    internal class RouterConnection
    {
        private readonly TcpClient _client; //The TCP/IP connection associated with this connection.
        private readonly Thread _dataPumpThread; //The thread that receives data for this connection.
        private readonly NetworkStream _stream; //The stream for the TCP/IP connection (used for reading and writing).
        private readonly RouterListener _listener; //The listener which owns this connection.
        private RouterConnection? _peer; //The associated endpoint connection for this connection.
        private bool _keepRunning;

        public Guid Id { get; private set; }
        public DateTime StartDateTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime { get; private set; } = DateTime.UtcNow;

        public RouterConnection(RouterListener listener, TcpClient tcpClient)
        {
            Id = Guid.NewGuid();
            _client = tcpClient;
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

        public void RunInboundAsync()
        {
            var endpoint = _listener.Router.Route.Endpoints.Collection.First();

            //Make the outbound connection to the endpoint specified for this route.
            var tcpClient = new TcpClient(endpoint.Address, endpoint.Port);
            _peer = new RouterConnection(_listener, tcpClient);

            _peer.RunOutboundAsync(this);

            //If we were successful making the outbound connection, then start the inbound connection thread.
            _dataPumpThread.Start();
        }

        public void RunOutboundAsync(RouterConnection peer)
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
            try { _client.Close(); } catch { }

            if (waitOnThread)
            {
                _dataPumpThread.Join();
            }
        }
    }
}
