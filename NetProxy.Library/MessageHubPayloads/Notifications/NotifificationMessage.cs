using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationMessage : IFrameNotification
    {
        public string Text { get; set; }

        public NotifificationMessage(string text)
        {
            Text = text;
        }
    }
}
