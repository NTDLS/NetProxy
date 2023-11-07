using NetProxy.Library.Routing;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryUserList : IFrameQuery
    {
    }

    public class QueryUserListReply : IFrameQueryReply
    {
        public string? Message { get; set; }
        public List<NpUser> Collection { get; set; } = new();
    }
}
