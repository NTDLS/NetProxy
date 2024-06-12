using NetProxy.Library.Payloads;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyConfigurationList : IRmQuery<QueryProxyConfigurationListReply>
    {
    }

    public class QueryProxyConfigurationListReply : IRmQueryReply
    {
        public string? Message { get; set; }

        public List<NpProxyGridItem> Collection { get; set; } = new();
    }
}
