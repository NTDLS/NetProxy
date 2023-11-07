using NetProxy.Library.Payloads;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryProxyConfigurationList : IFramePayloadQuery
    {
    }

    public class QueryProxyConfigurationListReply : IFramePayloadQueryReply
    {
        public string? Message { get; set; }

        public List<NpProxyGridItem> Collection { get; set; } = new();
    }
}
