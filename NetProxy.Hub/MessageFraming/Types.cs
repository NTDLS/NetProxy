using NetProxy.MessageHub.MessageFraming.Payloads;

namespace NetProxy.Hub.MessageFraming
{
    internal class Types
    {
        public delegate void ProcessFrameNotification(IFramePayloadNotification payload);

        public delegate IFramePayloadReply ProcessFrameQuery(IFramePayloadQuery payload);

        internal static class NtFrameDefaults
        {
            public const int FRAME_DELIMITER = 948724593;
            public const int FRAME_HEADER_SIZE = 10;
        }
    }
}
