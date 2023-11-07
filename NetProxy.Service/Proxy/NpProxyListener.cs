using Microsoft.Extensions.Caching.Memory;
using NetProxy.Library.Utilities;
using NTDLS.Semaphore;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    internal class NpProxyListener
    {
        private readonly TcpListener _listener;
        private Thread? _thread;
        private bool _keepRunning;
        private readonly CriticalResource<Dictionary<Guid, NpProxyConnection>> _activeConnections = new();

        internal NpProxy Proxy { get; private set; }
        internal MemoryCache StickySessionCache { get; private set; } = new(new MemoryCacheOptions());
        internal int LastTriedEndpointIndex { get; set; } = 0;
        internal CriticalResource<Dictionary<Guid, NpProxyEndpointStatistics>> EndpointStatistics { get; private set; } = new();

        public NpProxyListener(NpProxy proxy, TcpListener listener)
        {
            Proxy = proxy;
            _listener = listener;
          

            EndpointStatistics.Use((o) =>
            {
                //Add empty endpoint statistics for each endpoint.
                Proxy.Configuration.Endpoints.Collection.ForEach(e => o.Add(e.Id, new NpProxyEndpointStatistics()));
            });
        }

        public void StartAsync()
        {
            _keepRunning = true;
            _thread = new Thread(InboundListenerThreadProc);
            _thread.Start();
        }

        public void Stop()
        {
            NpUtility.TryAndIgnore(_listener.Stop);

            EndpointStatistics.Use(o => o.Clear());

            _activeConnections.Use((o) =>
            {
                foreach (var connection in o)
                {
                    connection.Value.Stop(true);
                }
                o.Clear();
            });

            _keepRunning = false;
            _thread?.Join();
            _thread = null;
        }

        public void RemoveActiveConnection(NpProxyConnection connection)
        {
            _activeConnections.Use((o) =>
            {
                if (o.Remove(connection.Id))
                {
                    Proxy.Statistics.Use((o) => { o.CurrentConnections--; });
                }
                connection.Stop(false);
            });

        }

        void InboundListenerThreadProc()
        {
            Thread.CurrentThread.Name = $"InboundListenerThreadProc:{Thread.CurrentThread.ManagedThreadId}:{Proxy.Configuration.Name}";

            try
            {
                _listener.Start();

                Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Listening inbound '{Proxy.Configuration.Name}' on port {Proxy.Configuration.ListenPort}");

                while (_keepRunning)
                {
                    var tcpClient = _listener.AcceptTcpClient(); //Wait for an inbound connection.

                    if (tcpClient.Connected)
                    {
                        if (_keepRunning) //Check again, we may have received a connection while shutting down.
                        {
                            var activeConnection = new NpProxyConnection(this, tcpClient);

                            _activeConnections.Use((o) => o.Add(activeConnection.Id, activeConnection));

                            Proxy.Statistics.Use((o) =>
                            {
                                o.CurrentConnections++;
                                o.TotalConnections++;
                            });

                            Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Accepted inbound endpoint connection: {activeConnection.Id}");
                            activeConnection.RunInboundAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(NpLogging.Severity.Exception, $"InboundConnectionThreadProc: {ex.Message}");
            }
            finally
            {
            }
        }
    }
}
