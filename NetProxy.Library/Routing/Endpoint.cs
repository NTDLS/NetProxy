namespace NetProxy.Library.Routing
{
    public class Endpoint
    {
        public bool Enabled { get; set; }
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Description { get; set; } = string.Empty;

        public Endpoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        public Endpoint()
        {

        }
    }
}
