using NetProxy.Library.Payloads;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyStatsistics : IFrameQuery
    {
    }

    public class QueryProxyStatsisticsReply : IFrameQueryReply
    {
        public string? Message { get; set; }
        public List<NpProxyGridStats> Collection { get; set; } = new();
    }
}
