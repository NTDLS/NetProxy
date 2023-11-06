using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;

namespace NetProxy.Service.Proxy
{
    public class NpProxyCollection : List<NpProxy>
    {
        public List<NpProxyConfiguration> CloneConfigurations()
        {
            var proxyConfigurations = new List<NpProxyConfiguration>();
            ForEach(o => proxyConfigurations.Add(o.Configuration));
            return proxyConfigurations;
        }

        public NpProxy? this[Guid proxyId]
            => this.Where(o => o.Configuration.Id == proxyId).FirstOrDefault();

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
                if (proxy.Configuration.AutoStart)
                {
                    try
                    {
                        proxy.Start();
                    }
                    catch (Exception ex)
                    {
                        Singletons.Logging.Write(new NpLogging.LoggingPayload
                        {
                            Severity = NpLogging.Severity.Exception,
                            CustomText = "Failed to start proxy.",
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
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to stop proxy.",
                        Exception = ex
                    });
                }
            }
        }
    }
}
