namespace NetProxy.Service.Proxy
{
    public class NpStickySession
    {
        public string Address { get; set; }
        public int Port { get; set; }

        public NpStickySession(string address, int port)
        {
            Address = address;
            Port = port;
        }
    }
}
