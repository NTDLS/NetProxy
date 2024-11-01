using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationPersistUserList(List<NpUser> collection) : IRmNotification
    {
        public List<NpUser> Collection { get; set; } = collection;
    }
}
