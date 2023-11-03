namespace NetProxy.Library.Payloads
{
    public class RouteGridStats
    {
        public Guid Id { get; set; }
        public bool IsRunning { get; set; }
        public ulong TotalConnections { get; set; }
        public int CurrentConnections { get; set; }
        public ulong BytesSent { get; set; }
        public ulong BytesReceived { get; set; }
    }
}
