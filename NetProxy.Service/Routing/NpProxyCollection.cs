using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;

namespace NetProxy.Service.Routing
{
    public class NpProxyCollection : List<NpProxy>
    {
        public List<NpRoute> Routes()
        {
            var routes = new List<NpRoute>();
            this.ForEach(o => routes.Add(o.Route));
            return routes;
        }

        public NpProxy? this[Guid routeId]
            => this.Where(o => o.Route.Id == routeId).FirstOrDefault();

        //public void Add(NpProxy proxy)
        //    => this.Add(proxy);

        public new void Remove(NpProxy item)
        {
            NpUtility.TryAndIgnore(item.Stop);
            base.Remove(item);
        }

        public void Start()
        {
            foreach (var proxy in this)
            {
                if (proxy.Route.AutoStart)
                {
                    try
                    {
                        proxy.Start();
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
            foreach (var proxy in this)
            {
                try
                {
                    proxy.Stop();
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
