using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Payloads.Routing
{
    public class NpProxyConfiguration
    {
        public Guid Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public BindingProtocol BindingProtocol { get; set; } = BindingProtocol.IPv4;

        /// <summary>
        /// What the listener accepts. Https terminates TLS at the listener using the certificate below.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
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

        /// <summary>
        /// Path (on the machine running the service) of the PFX/PKCS#12 certificate the listener presents to clients (Https traffic type).
        /// </summary>
        public string TlsCertificatePath { get; set; } = string.Empty;

        /// <summary>
        /// Password of the PFX certificate, if it has one. Stored encrypted by the service.
        /// </summary>
        public string TlsCertificatePassword { get; set; } = string.Empty;

        public NpProxyConfiguration()
        {
            Id = Guid.NewGuid();
            AcceptBacklogSize = Constants.DefaultAcceptBacklogSize;
            InitialBufferSize = Constants.DefaultInitialBufferSize;
            MaxBufferSize = Constants.DefaultMaxBufferSize;
            StickySessionCacheExpiration = Constants.DefaultStickySessionExpiration;
            Bindings = new List<NpBinding>();
        }

        /// <summary>
        /// A copy that can be changed or stored without affecting the one in use.
        /// </summary>
        public NpProxyConfiguration Clone()
            => JsonConvert.DeserializeObject<NpProxyConfiguration>(JsonConvert.SerializeObject(this))
                ?? throw new Exception("The proxy configuration could not be copied.");
    }
}
