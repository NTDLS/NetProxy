namespace NetProxy.Service
{
    public class RoutingServices
    {
        private readonly Management _management;

        public RoutingServices()
        {
            _management = new Management();
        }

        public void Start()
        {
            _management.Start();
        }

        public void Stop()
        {
            _management.Stop();
        }
    }
}
