using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationDeleteProxy : IFrameNotification
    {
        public Guid Id { get; set; }

        public NotifificationDeleteProxy(Guid id)
        {
            Id = id;
        }
    }
}
