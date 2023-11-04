using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;

namespace NetProxy.Service.Routing
{
    public class NpRouters
    {
        public List<NpRouter> Collection = new();

        public List<NpRoute> Routes()
        {
            var routes = new List<NpRoute>();
            Collection.ForEach(o => routes.Add(o.Route));
            return routes;
        }

        public NpRouter? this[Guid routeId]
            => Collection.Where(o => o.Route.Id == routeId).FirstOrDefault();

        public void Add(NpRouter router)
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
                        Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                        {
                            Severity = NpLogging.Severity.Exception,
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
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to stop route.",
                        Exception = ex
                    });
                }
            }
        }
    }
}
