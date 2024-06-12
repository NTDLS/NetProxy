using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public class QueryLogin : IRmQuery<QueryLoginReply>
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public QueryLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }

    public class QueryLoginReply : IRmQueryReply
    {
        public bool Result { get; set; } = false;
        public string? Message { get; set; }
    }
}
