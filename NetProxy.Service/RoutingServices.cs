using NetProxy.Library;
using NetProxy.Library.Routing;
using NetProxy.Hub;
using NetProxy.Service.Routing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace NetProxy.Service
{
    /// <summary>
    /// This is a stub for the upcomming windows service.
    /// </summary>
    public class RoutingServices
    {
        Management management;

        public RoutingServices()
        {
            management = new Management();
        }

        public void Start()
        {
            management.Start();
        }

        public void Stop()
        {
            management.Stop();
        }
    }
}
