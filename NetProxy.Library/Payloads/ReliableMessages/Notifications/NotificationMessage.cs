using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationMessage(string text) : IRmNotification
    {
        public string Text { get; set; } = text;
    }
}
