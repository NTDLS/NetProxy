using NetProxy.Library.Utilities;

namespace NetProxy.Service.Proxy
{
    public class NpProxyCollection : List<NpProxy>
    {
        public new void Remove(NpProxy item)
        {
            try
            {
                item.Stop();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to stop proxy", ex);
            }

            base.Remove(item);
        }

        /// <summary>
        /// Starts the proxies that are set to start automatically. One that fails to start does not stop the others.
        /// </summary>
        public void Start()
        {
            foreach (var proxy in this.ToList())
            {
                if (proxy.Configuration.AutoStart)
                {
                    if (proxy.Start(out var error) == false)
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Warning, $"Proxy '{proxy.Configuration.Name}' was not started: {error}");
                    }
                }
            }
        }

        public void Stop()
        {
            foreach (var proxy in this.ToList())
            {
                try
                {
                    proxy.Stop();
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write($"Failed to stop proxy '{proxy.Configuration.Name}'", ex);
                }
            }
        }
    }
}
