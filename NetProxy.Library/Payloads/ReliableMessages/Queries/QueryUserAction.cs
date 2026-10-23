using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public enum NpUserAction { Add, Remove, SetPassword }

    /// <summary>
    /// Adds a user, removes one, or sets a password. The password is sent as the hash that the login uses, so it is never in the clear.
    /// </summary>
    public class QueryUserAction : IRmQuery<QueryActionReply>
    {
        public NpUserAction Action { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
