using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationRegisterLogin : IFramePayloadNotification
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public NotifificationRegisterLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }
}
