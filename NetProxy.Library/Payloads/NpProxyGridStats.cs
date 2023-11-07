namespace NetProxy.Library.Payloads
{
    public class NpProxyGridStats
    {
        public Guid Id { get; set; }
        public bool IsRunning { get; set; }
        public ulong TotalConnections { get; set; }
        public int CurrentConnections { get; set; }
        public ulong BytesWritten { get; set; }
        public ulong BytesRead { get; set; }
    }
}
