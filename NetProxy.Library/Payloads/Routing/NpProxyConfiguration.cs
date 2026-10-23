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
        /// Offer HTTP/2 to clients (and so gRPC). With the Https traffic type it is negotiated with the client (ALPN) and the endpoint
        /// has to speak it too; with the Http traffic type a client that starts with the HTTP/2 preface (h2c) is detected.
        /// Header rules are applied to HTTP/2 requests and responses as well.
        /// </summary>
        public bool AllowHttp2 { get; set; }

        /// <summary>
        /// The most connections the proxy will have open at once. Further connections are closed as they are accepted. 0 is no limit.
        /// </summary>
        public int MaxConnections { get; set; }

        /// <summary>
        /// The most connections that one client address will have open at once. 0 is no limit.
        /// </summary>
        public int MaxConnectionsPerClient { get; set; }

        /// <summary>
        /// A connection that has not carried any data in either direction for this many seconds is closed. 0 is no timeout.
        /// </summary>
        public int IdleTimeoutSeconds { get; set; }

        /// <summary>
        /// Path (on the machine running the service) of the PFX/PKCS#12 certificate the listener presents to clients (Https traffic type).
        /// </summary>
        public string TlsCertificatePath { get; set; } = string.Empty;

        /// <summary>
        /// Password of the PFX certificate, if it has one. Stored encrypted by the service.
        /// </summary>
        public string TlsCertificatePassword { get; set; } = string.Empty;

        /// <summary>
        /// The host names that this proxy answers to. Proxies (of the HTTP or HTTPS traffic type) that listen on the same port tell
        /// the requests apart by these. A proxy with none is the default for the port: it gets the requests that no other proxy claims.
        /// </summary>
        public List<NpHostName> HostNames { get; set; } = new();

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
