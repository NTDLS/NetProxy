using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationStopProxy : IRmNotification
    {
        public Guid Id { get; set; }

        public NotificationStopProxy(Guid id)
        {
            Id = id;
        }
    }
}
