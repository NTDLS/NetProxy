using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
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
