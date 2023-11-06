using NetProxy.MessageHub.MessageFraming.Payloads;

namespace NetProxy.Hub.MessageFraming
{
    internal class QueryAwaitingReply
    {
        public Guid FrameId { get; set; }
        public AutoResetEvent WaitEvent { get; set; } = new(false);
        public IFramePayloadReply? ReplyPayload { get; set; }
    }
}
