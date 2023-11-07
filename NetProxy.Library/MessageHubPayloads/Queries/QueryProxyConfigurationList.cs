using NetProxy.Library.Payloads;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyConfigurationList : IFrameQuery
    {
    }

    public class QueryProxyConfigurationListReply : IFrameQueryReply
    {
        public string? Message { get; set; }

        public List<NpProxyGridItem> Collection { get; set; } = new();
    }
}
