using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationStartProxy(Guid id) : IRmNotification
    {
        public Guid Id { get; set; } = id;
    }
}
