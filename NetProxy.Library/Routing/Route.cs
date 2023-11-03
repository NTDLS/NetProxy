using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace NetProxy.Library.Routing
{
    public class Route
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public BindingProtocal BindingProtocal { get; set; }

        public TrafficType TrafficType { get; set; }
        public bool AutoStart { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int ListenPort { get; set; }
        public int SpinLockCount { get; set; }
        public bool UseStickySessions { get; set; }
        public bool CompressBindingTunnel { get; set; }
        public bool CompressEndpointTunnel { get; set; }
        public bool EncryptBindingTunnel { get; set; }
        public bool EncryptEndpointTunnel { get; set; }
        public string BindingPreSharedKey { get; set; }
        public string EndpointPreSharedKey { get; set; }
        public bool BindingIsTunnel { get; set; }
        public bool EndpointIsTunnel { get; set; }
        public int AcceptBacklogSize { get; set; }
        public int EncryptionInitilizationTimeoutMs { get; set; }
        public int InitialBufferSize { get; set; }
        public int StickySessionCacheExpiration { get; set; }
        public int MaxBufferSize { get; set; }
        public string Description { get; set; }
        public List<Binding> Bindings { get; set; }
        public bool ListenOnAllAddresses { get; set; }

        public Endpoints Endpoints = new Endpoints();

        public HttpHeaderRules HttpHeaderRules = new HttpHeaderRules();

        public Route()
        {
            Id = Guid.NewGuid();
            AcceptBacklogSize = Constants.DefaultAcceptBacklogSize;
            InitialBufferSize = Constants.DefaultInitialBufferSize;
            MaxBufferSize = Constants.DefaultMaxBufferSize;
            StickySessionCacheExpiration = Constants.DefaultStickySessionExpiration;
            SpinLockCount = Constants.DefaultSpinLockCount;
            EncryptionInitilizationTimeoutMs = Constants.DefaultEncryptionInitilizationTimeoutMs;
            Bindings = new List<Binding>();
        }
    }
}