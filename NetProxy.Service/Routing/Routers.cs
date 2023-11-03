using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;

namespace NetProxy.Service.Routing
{
    public class Routers
    {
        public List<Router> Collection = new();

        public List<Route> Routes()
        {
            var routes = new List<Route>();
            Collection.ForEach(o => routes.Add(o.Route));
            return routes;
        }

        public Router? this[Guid routeId]
            => Collection.Where(o => o.Route.Id == routeId).FirstOrDefault();

        public void Add(Router router)
            => Collection.Add(router);

        public void Start()
        {
            foreach (var router in Collection)
            {
                if (router.Route.AutoStart)
                {
                    try
                    {
                        router.Start();
                    }
                    catch (Exception ex)
                    {
                        Singletons.EventLog.WriteLog(new Logging.LoggingPayload
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
            foreach (var router in Collection)
            {
                try
                {
                    router.Stop();
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new Logging.LoggingPayload
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
