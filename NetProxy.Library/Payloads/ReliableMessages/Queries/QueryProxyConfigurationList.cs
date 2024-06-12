using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
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
