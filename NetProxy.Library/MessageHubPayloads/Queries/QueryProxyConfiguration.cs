using NetProxy.Library.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyConfiguration : IRmQuery<QueryProxyConfigurationReply>
    {
        public Guid Id { get; set; }

        public QueryProxyConfiguration(Guid id)
        {
            Id = id;
        }
    }

    public class QueryProxyConfigurationReply : IRmQueryReply
    {
        public string? Message { get; set; }

        public NpProxyConfiguration ProxyConfiguration { get; set; } = new();
    }
}
