using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationPersistUserList : IRmNotification
    {
        public List<NpUser> Collection { get; set; }

        public NotificationPersistUserList(List<NpUser> collection)
        {
            Collection = collection;
        }
    }
}
