using NetProxy.Library.Routing;
using NetProxy.Library.Utility;

namespace NetProxy.Service.Routing
{
    public class Routers
    {
        public List<Router> List = new List<Router>();

        public List<Route> Routes()
        {
            var routes = new List<Route>();

            foreach (var router in List)
            {
                routes.Add(router.Route);
            }

            return routes;
        }

        public Router this[System.Guid routeId]
        {
            get
            {
                return (from o in List where o.Route.Id == routeId select o).FirstOrDefault();
            }
        }

        public void Add(Router router)
        {
            List.Add(router);
        }

        public void Start()
        {
            foreach (var router in List)
            {
                if (router.Route.AutoStart)
                {
                    try
                    {
                        router.Start();
                    }
                    catch (Exception ex)
                    {
                        Singletons.EventLog.WriteEvent(new Logging.EventPayload
                        {
                            Severity = Logging.Severity.Error,
                            CustomText = "Failed to start route.",
                            Exception = ex
                        });
                    }
                }
            }
        }

        public void Stop()
        {
            foreach (var router in List)
            {
                try
                {
                    router.Stop();
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
                        CustomText = "Failed to stop route.",
                        Exception = ex
                    });
                }
            }
        }

    }
}
