using NetProxy.Library.Routing;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryUserList : IFramePayloadQuery
    {
    }

    public class QueryUserListReply : IFramePayloadQueryReply
    {
        public string? Message { get; set; }
        public List<NpUser> Collection { get; set; } = new();
    }
}
