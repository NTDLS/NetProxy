using NetProxy.Library.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Queries
{
    public class QueryUserList : IRmQuery<QueryUserListReply>
    {
    }

    public class QueryUserListReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<NpUser> Collection { get; set; } = new();
    }
}
