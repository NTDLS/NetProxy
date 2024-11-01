using NetProxy.Library.Payloads.Routing;
using NTDLS.Helpers;

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

        public new void Remove(NpProxy item)
        {
            Exceptions.Ignore(item.Stop);
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
                        Singletons.Logging.Write("Failed to start proxy.", ex);
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
                    Singletons.Logging.Write("Failed to stop proxy.", ex);
                }
            }
        }
    }
}
