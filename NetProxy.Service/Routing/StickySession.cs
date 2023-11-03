namespace NetProxy.Service.Routing
{
    public class StickySession
    {
        public string DestinationAddress { get; set; } = string.Empty;
        public int DestinationPort { get; set; }
    }
}
