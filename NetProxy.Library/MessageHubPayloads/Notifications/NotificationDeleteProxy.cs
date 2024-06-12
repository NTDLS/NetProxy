using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
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
