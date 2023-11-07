using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationStopProxy : IFramePayloadNotification
    {
        public Guid Id { get; set; }

        public NotifificationStopProxy(Guid id)
        {
            Id = id;
        }
    }
}
