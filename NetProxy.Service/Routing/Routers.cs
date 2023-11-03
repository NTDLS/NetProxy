using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;

namespace NetProxy.Service.Routing
{
    public class Routers
    {
        public List<Router> List = new();

        public List<Route> Routes()
        {
            var routes = new List<Route>();

            foreach (var router in List)
            {
                routes.Add(router.Route);
            }

            return routes;
        }

        public Router? this[Guid routeId]
            => List.Where(o => o.Route.Id == routeId).FirstOrDefault();

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
