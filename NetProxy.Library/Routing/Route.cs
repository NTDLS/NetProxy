using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        public int EncryptionInitilizationTimeoutMS { get; set; }
        public int InitialBufferSize { get; set; }
        public int StickySessionCacheExpiration { get; set; }
        public int MaxBufferSize { get; set; }
        public string Description { get; set; }
        public List<Binding> Bindings = new List<Binding>();
        public bool ListenOnAllAddresses { get; set; }

        public Endpoints Endpoints = new Endpoints();

        public HttpHeaderRules HttpHeaderRules = new HttpHeaderRules();

        public Route()
        {
            Id = Guid.NewGuid();
            AcceptBacklogSize = Constants.DEFAULT_ACCEPT_BACKLOG_SIZE;
            InitialBufferSize = Constants.DEFAULT_INITIAL_BUFFER_SIZE;
            MaxBufferSize = Constants.DEFAULT_MAX_BUFFER_SIZE;
            StickySessionCacheExpiration = Constants.DEFAULT_STICKY_SESSION_EXPIRATION;
            SpinLockCount = Constants.DEFAULT_SPIN_LOCK_COUNT;
            EncryptionInitilizationTimeoutMS =  Constants.DEFAULT_ENCRYPTION_INITILIZATION_TIMEOUT_MS;
        }
    }
}
