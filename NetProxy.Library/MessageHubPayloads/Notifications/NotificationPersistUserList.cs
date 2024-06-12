using NetProxy.Library.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
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
