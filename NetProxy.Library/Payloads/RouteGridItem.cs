namespace NetProxy.Library.Payloads
{
    public class RouteGridItem: NetProxy.Library.Routing.Route
    {
        public string RouterType { get; set; }
        public bool IsRunning { get; set; }
    }
}
