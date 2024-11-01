using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public class QueryLogin(string userName, string passwordHash) : IRmQuery<QueryLoginReply>
    {
        public string UserName { get; set; } = userName;
        public string PasswordHash { get; set; } = passwordHash;
    }

    public class QueryLoginReply : IRmQueryReply
    {
        public bool Result { get; set; } = false;
        public string? Message { get; set; }
    }
}
