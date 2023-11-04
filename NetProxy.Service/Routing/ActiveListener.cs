using NetProxy.Library.Utilities;
using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    internal class ActiveListener
    {
        internal readonly Dictionary<Guid, ActiveConnection> _activeConnections = new();

        private readonly TcpListener _listener;
        private readonly Thread _thread;
        private bool _keepRunning;
        private readonly Router _router;

        public ActiveListener(Router router, TcpListener listener)
        {
            _router = router;
            _listener = listener;
            _thread = new Thread(InboundConnectionThreadProc);
        }

        public void StartAsync()
        {
            _keepRunning = true;
            _thread.Start();
        }

        public void Stop()
        {
            _keepRunning = false;
            _thread.Join();
        }

        void InboundConnectionThreadProc()
        {
            Thread.CurrentThread.Name = $"InboundConnectionThreadProc:{Thread.CurrentThread.ManagedThreadId}";
            try
            {
                _listener.Start();

                Singletons.EventLog.WriteLog(Logging.Severity.Verbose, $"Listening inbound '{_router.Route.Name}' on port {_router.Route.ListenPort}");

                while (_keepRunning)
                {
                    var tcpClient = _listener.AcceptTcpClient(); //Wait for an inbound connection.

                    if (tcpClient.Connected)
                    {
                        if (_keepRunning) //Check again, we may have received a connection while shutting down.
                        {
                            var activeConnection = new ActiveConnection(_router, tcpClient);
                            _activeConnections.Add(activeConnection.Id, activeConnection);

                            Singletons.EventLog.WriteLog(Logging.Severity.Verbose, $"Accepted inbound endpoint connection: {activeConnection.Id}");
                            activeConnection.RunInboundAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(Logging.Severity.Exception, $"InboundConnectionThreadProc: {ex.Message}");
            }
            finally
            {
            }
        }
    }
}
