namespace NetProxy.Library.Payloads
{
    public class NpProxyGridItem : Routing.NpProxyConfiguration
    {
        public string ProxyType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
