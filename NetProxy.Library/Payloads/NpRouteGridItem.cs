namespace NetProxy.Library.Payloads
{
    public class NpRouteGridItem : Routing.NpRoute
    {
        public string RouterType { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
    }
}
