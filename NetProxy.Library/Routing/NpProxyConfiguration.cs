using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Routing
{
    public class NpProxyConfiguration
    {
        public Guid Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public BindingProtocol BindingProtocol { get; set; }
        public TrafficType TrafficType { get; set; }
        public bool AutoStart { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public bool UseStickySessions { get; set; }
        public int AcceptBacklogSize { get; set; }
        public int InitialBufferSize { get; set; }
        public int StickySessionCacheExpiration { get; set; }
        public int MaxBufferSize { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<NpBinding> Bindings { get; set; }
        public bool ListenOnAllAddresses { get; set; }
        public NpEndpoints Endpoints { get; set; } = new();
        public NpHTTPHeaderRules HttpHeaderRules { get; set; } = new();

        public NpProxyConfiguration()
        {
            Id = Guid.NewGuid();
            AcceptBacklogSize = Constants.DefaultAcceptBacklogSize;
            InitialBufferSize = Constants.DefaultInitialBufferSize;
            MaxBufferSize = Constants.DefaultMaxBufferSize;
            StickySessionCacheExpiration = Constants.DefaultStickySessionExpiration;
            Bindings = new List<NpBinding>();
        }
    }
}
