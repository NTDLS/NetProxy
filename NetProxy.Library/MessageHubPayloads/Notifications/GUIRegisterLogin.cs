using NetProxy.MessageHub.MessageFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class GUIRegisterLogin : IFramePayloadNotification
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public GUIRegisterLogin(string userName, string passwordHash)
        {
            UserName = userName;
            PasswordHash = passwordHash;
        }
    }
}
