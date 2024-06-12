using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotificationStartProxy : IRmNotification
    {
        public Guid Id { get; set; }

        public NotificationStartProxy(Guid id)
        {
            Id = id;
        }
    }
}
