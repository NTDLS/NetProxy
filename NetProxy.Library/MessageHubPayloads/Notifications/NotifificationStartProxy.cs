using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationStartProxy : IFramePayloadNotification
    {
        public Guid Id { get; set; }

        public NotifificationStartProxy(Guid id)
        {
            Id = id;
        }
    }
}
