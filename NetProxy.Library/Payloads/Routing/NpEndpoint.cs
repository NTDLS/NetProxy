using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Payloads.Routing
{
    public class NpEndpoint
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool Enabled { get; set; } = true;
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// What is spoken to this endpoint. Https makes the proxy start TLS with the endpoint (validating its certificate against the
        /// address), whatever the traffic type of the listener is. Tcp and Http send the connection through as it is.
        /// Configurations from before this existed have no value, which is Tcp: the same as it always was.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public TrafficType TrafficType { get; set; } = TrafficType.Tcp;

        /// <summary>
        /// When false, the endpoint's certificate is not validated (Https endpoints only).
        /// </summary>
        public bool TlsValidateSSLCertificate { get; set; }

        public NpEndpoint(string address, int port)
        {
            Address = address;
            Port = port;
        }

        public NpEndpoint()
        {
        }
    }
}
