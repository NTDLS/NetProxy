using System.ServiceProcess;

namespace NetProxy.Service
{
    partial class NetworkDlsNetProxyService : ServiceBase
    {
        RoutingServices _routingServices = new RoutingServices();

        public NetworkDlsNetProxyService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _routingServices.Start();
        }

        protected override void OnStop()
        {
            _routingServices.Stop();
        }
    }
}
