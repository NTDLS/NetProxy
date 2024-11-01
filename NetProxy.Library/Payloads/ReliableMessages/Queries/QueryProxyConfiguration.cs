using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public class QueryProxyConfiguration(Guid id) : IRmQuery<QueryProxyConfigurationReply>
    {
        public Guid Id { get; set; } = id;
    }

    public class QueryProxyConfigurationReply : IRmQueryReply
    {
        public string? Message { get; set; }

        public NpProxyConfiguration ProxyConfiguration { get; set; } = new();
    }
}
