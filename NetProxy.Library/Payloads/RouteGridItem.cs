namespace NetProxy.Library.Payloads
{
    public class RouteGridItem : NetProxy.Library.Routing.Route
    {
        public string RouterType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
