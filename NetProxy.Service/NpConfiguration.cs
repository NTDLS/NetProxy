using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.Routing;

namespace NetProxy.Service
{
    public class NpConfiguration
    {
        public int ManagementPort { get; set; }
        public NpUsers Users { get; set; } = new();

        /// <summary>
        /// Logging and other settings of the service itself. Files from before these existed have none, which gets the defaults.
        /// </summary>
        public NpServiceSettings Settings { get; set; } = new();
    }
}
