using NetProxy.Library.Routing;

namespace NetProxy.Service
{
    public class Configuration
    {
        public int ManagementPort { get; set; }
        public Users Users = new Users();
    }
}
