using NetProxy.Library.Payloads.Routing;

namespace NetProxy.Library.Payloads
{
    public class NpProxyGridItem : NpProxyConfiguration
    {
        public string ProxyType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
