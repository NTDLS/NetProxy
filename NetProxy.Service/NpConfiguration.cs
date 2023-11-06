using NetProxy.Library.Routing;

namespace NetProxy.Service
{
    public class NpConfiguration
    {
        public int ManagementPort { get; set; }
        public NpUsers Users = new();
    }
}
