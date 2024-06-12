using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
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
