using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationDeleteProxy : IRmNotification
    {
        public Guid Id { get; set; }

        public NotificationDeleteProxy(Guid id)
        {
            Id = id;
        }
    }
}
