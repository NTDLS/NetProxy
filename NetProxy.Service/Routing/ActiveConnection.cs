using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    internal class ActiveConnection : IDisposable
    {
        private TcpClient _client;
        private Thread _thread;
        private bool _keepRunning;
        private readonly NetworkStream _stream;
        private readonly Router _router;
        private ActiveConnection? _peer;

        public Guid Id { get; private set; }
        public DateTime StartDateTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastActivityDateTime { get; private set; } = DateTime.UtcNow;
        public double ActivityAgeInMiliseconds => (DateTime.UtcNow - LastActivityDateTime).TotalMilliseconds;
        public double StartAgeInMiliseconds => (DateTime.UtcNow - StartDateTime).TotalMilliseconds;

        public ActiveConnection(Router router, TcpClient tcpClient)
        {
            _router = router;
            _thread = new Thread(DataPumpThread);
            _client = tcpClient;
            Id = Guid.NewGuid();
            _stream = tcpClient.GetStream();
            _keepRunning = true;
        }

        public void Disconnect()
        {
            try { _stream.Close(); } catch { }
            try { _client.Close(); } catch { }
            _keepRunning = false;
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
            var endpoint = _router.Route.Endpoints.Collection.First();

            //Make the outbound connection to the endpoint specified for this route.
            var tcpClient = new TcpClient(endpoint.Address, endpoint.Port);
            _peer = new ActiveConnection(_router, tcpClient);

            _peer.RunOutboundAsync(this);

            //If we were successful making the outbound connection, then start the inbound connection thread.
            _thread.Start();
        }

        public void RunOutboundAsync(ActiveConnection peer)
        {
            _peer = peer; //Each active connection needs a reference to the opposite endpoint connection.
            _thread.Start();
        }

        internal void DataPumpThread()
        {
            byte[] buffer = new byte[_router.Route.InitialBufferSize];

            while (_keepRunning)
            {
                if (Read(ref buffer, out int length))
                {
                    _peer?.Write(buffer, length);
                }
            }
        }

        public void Dispose()
        {
            Disconnect();

            try { _stream.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
        }
    }
}
