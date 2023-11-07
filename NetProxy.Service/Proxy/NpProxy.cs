using NetProxy.Library.Routing;
using NTDLS.Semaphore;
using System.Net;
using System.Net.Sockets;

namespace NetProxy.Service.Proxy
{
    public class NpProxy
    {
        internal CriticalResource<NpProxyStatistics> Statistics { get; private set; } = new();

        public NpProxyConfiguration Configuration { get; private set; }
        private readonly List<NpProxyListener> _listeners = new();
        private bool _keepRunning = false;

        public bool IsRunning => _keepRunning;

        public NpProxy(NpProxyConfiguration configuration)
        {
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
            if (_keepRunning == false)
            {
                return;
            }
            _keepRunning = false;

            foreach (var listener in _listeners)
            {
                listener.Stop();
            }
        }
    }
}
