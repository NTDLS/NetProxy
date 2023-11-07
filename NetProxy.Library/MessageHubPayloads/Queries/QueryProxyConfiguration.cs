using NetProxy.Library.Routing;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyConfiguration : IFrameQuery
    {
        public Guid Id { get; set; }

        public QueryProxyConfiguration(Guid id)
        {
            Id = id;
        }
    }

    public class QueryProxyConfigurationReply : IFrameQueryReply
    {
        public string? Message { get; set; }

        public NpProxyConfiguration ProxyConfiguration { get; set; } = new();
    }
}
