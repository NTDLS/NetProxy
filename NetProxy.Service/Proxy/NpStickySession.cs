namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Remembers which endpoint a client was sent to.
    /// </summary>
    public class NpStickySession
    {
        public Guid EndpointId { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }

        public NpStickySession(Guid endpointId, string address, int port)
        {
            EndpointId = endpointId;
            Address = address;
            Port = port;
        }
    }
}
