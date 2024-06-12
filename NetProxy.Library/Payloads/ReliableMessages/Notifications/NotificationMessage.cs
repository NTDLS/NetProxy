using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationMessage : IRmNotification
    {
        public string Text { get; set; }

        public NotificationMessage(string text)
        {
            Text = text;
        }
    }
}
