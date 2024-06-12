using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotificationRegisterLogin : IRmNotification
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public NotificationRegisterLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }
}
