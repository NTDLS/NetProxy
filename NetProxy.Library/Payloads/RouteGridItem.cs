namespace NetProxy.Library.Payloads
{
    public class RouteGridItem : Routing.Route
    {
        public string RouterType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
