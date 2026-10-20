namespace NetProxy.Library.Payloads
{
    /// <summary>
    /// One open connection of a proxy, as shown in the connection list of the console. "In" is what the client sent to the
    /// endpoint, "out" is what the endpoint sent back to the client.
    /// </summary>
    public class NpConnectionGridItem
    {
        public Guid Id { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime LastActivityDateTime { get; set; }

        public string SourceAddress { get; set; } = string.Empty;
        public string? SourceHostName { get; set; }
        public int SourcePort { get; set; }

        /// <summary>
        /// The address and port on the proxy that the client connected to.
        /// </summary>
        public string ListenAddress { get; set; } = string.Empty;
        public int ListenPort { get; set; }

        /// <summary>
        /// Empty until the connection to the endpoint is made.
        /// </summary>
        public string DestinationAddress { get; set; } = string.Empty;
        public string? DestinationHostName { get; set; }
        public int DestinationPort { get; set; }

        /// <summary>
        /// What the proxy treats the traffic as: Raw, Http or Https.
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// The version of the application protocol (HTTP/1.1, HTTP/2), when the proxy can tell.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// The TLS version of the client side and of the endpoint side, when each is encrypted.
        /// </summary>
        public string ClientTls { get; set; } = string.Empty;
        public string EndpointTls { get; set; } = string.Empty;

        public bool IsConnectedToEndpoint { get; set; }
        public ulong BytesIn { get; set; }
        public ulong BytesOut { get; set; }
    }
}
