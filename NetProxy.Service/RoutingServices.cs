namespace NetProxy.Service
{
    public class RoutingServices
    {
        Management management;

        public RoutingServices()
        {
            management = new Management();
        }

        public void Start()
        {
            management.Start();
        }

        public void Stop()
        {
            management.Stop();
        }
    }
}
