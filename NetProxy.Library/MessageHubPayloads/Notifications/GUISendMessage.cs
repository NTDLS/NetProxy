using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class GUISendMessage : IFramePayloadNotification
    {
        public string Message { get; set; }

        public GUISendMessage(string message)
        {
            Message = message;

        }
    }
}
