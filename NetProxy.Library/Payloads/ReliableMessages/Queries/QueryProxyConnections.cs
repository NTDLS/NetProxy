using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public class QueryProxyConnections(Guid proxyId) : IRmQuery<QueryProxyConnectionsReply>
    {
        public Guid ProxyId { get; set; } = proxyId;
    }

    public class QueryProxyConnectionsReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<NpConnectionGridItem> Collection { get; set; } = new();
    }
}
