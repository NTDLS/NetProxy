using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library.Utilities;
using NTDLS.Semaphore;
using System.Net.Sockets;

namespace NetProxy.Service.Routing
{
    internal class NpRouterListener
    {
        internal readonly CriticalResource<Dictionary<Guid, NpRouterConnection>> _activeConnections = new();

        private readonly TcpListener _listener;
        private readonly Thread _thread;
        private bool _keepRunning;

        public NpRouter Router { get; private set; }
        public MemoryCache StickySessionCache { get; private set; } = new(new MemoryCacheOptions());

        public NpRouterListener(NpRouter router, TcpListener listener)
        {
            Router = router;
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
            NpUtility.TryAndIgnore(_listener.Stop);

            _activeConnections.Use((o) =>
            {
                foreach (var connection in o)
                {
                    connection.Value.Stop(true);
                }
                o.Clear();
            });

            _keepRunning = false;
            _thread.Join();
        }

        public void RemoveActiveConnection(NpRouterConnection connection)
        {
            _activeConnections.Use((o) =>
            {
                o.Remove(connection.Id);
                connection.Stop(false);
            });
        }

        void InboundConnectionThreadProc()
        {
            Thread.CurrentThread.Name = $"InboundConnectionThreadProc:{Thread.CurrentThread.ManagedThreadId}";
            try
            {
                _listener.Start();

                Singletons.EventLog.WriteLog(NpLogging.Severity.Verbose, $"Listening inbound '{Router.Route.Name}' on port {Router.Route.ListenPort}");

                while (_keepRunning)
                {
                    var tcpClient = _listener.AcceptTcpClient(); //Wait for an inbound connection.

                    if (tcpClient.Connected)
                    {
                        if (_keepRunning) //Check again, we may have received a connection while shutting down.
                        {
                            var activeConnection = new NpRouterConnection(this, tcpClient);

                            _activeConnections.Use((o) => o.Add(activeConnection.Id, activeConnection));

                            Singletons.EventLog.WriteLog(NpLogging.Severity.Verbose, $"Accepted inbound endpoint connection: {activeConnection.Id}");
                            activeConnection.RunInboundAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(NpLogging.Severity.Exception, $"InboundConnectionThreadProc: {ex.Message}");
            }
            finally
            {
            }
        }
    }
}
