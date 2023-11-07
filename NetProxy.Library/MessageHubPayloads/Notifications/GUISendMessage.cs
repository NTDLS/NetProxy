using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class GUISendMessage : IFramePayloadNotification
    {
        public string Text { get; set; }

        public GUISendMessage(string text)
        {
            Text = text;
        }
    }
}
