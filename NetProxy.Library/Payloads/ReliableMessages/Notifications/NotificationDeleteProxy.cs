using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationDeleteProxy(Guid id) : IRmNotification
    {
        public Guid Id { get; set; } = id;
    }
}
