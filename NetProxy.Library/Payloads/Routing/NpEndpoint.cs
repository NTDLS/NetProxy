namespace NetProxy.Library.Payloads.Routing
{
    public class NpEndpoint
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool Enabled { get; set; }
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Description { get; set; } = string.Empty;

        public NpEndpoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        public NpEndpoint()
        {

        }
    }
}
