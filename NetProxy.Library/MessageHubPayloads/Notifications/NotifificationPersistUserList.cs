using NetProxy.Library.Routing;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationPersistUserList : IFramePayloadNotification
    {
        public List<NpUser> Collection { get; set; }

        public NotifificationPersistUserList(List<NpUser> collection)
        {
            Collection = collection;
        }
    }
}
