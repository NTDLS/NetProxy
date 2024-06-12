using NetProxy.Library.Payloads;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyStatistics : IRmQuery<QueryProxyStatisticsReply>
    {
    }

    public class QueryProxyStatisticsReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<NpProxyGridStats> Collection { get; set; } = new();
    }
}
