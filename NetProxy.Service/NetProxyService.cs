using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace NetProxy.Service
{
    partial class NetworkDLSNetProxyService : ServiceBase
    {
        RoutingServices routingServices = new RoutingServices();

        public NetworkDLSNetProxyService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            routingServices.Start();
        }

        protected override void OnStop()
        {
            routingServices.Stop();
        }
    }
}
