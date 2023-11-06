using NetProxy.MessageHub.MessageFraming.Payloads;
using NetProxy.Service.Proxy;

namespace NetProxy.Hub
{
    internal interface IHub
    {
        public void InvokeOnDisconnected(Guid connectionId);
        internal void InvokeOnNotificationReceived(Guid connectionId, IFramePayloadNotification payload);
        internal IFramePayloadReply InvokeOnQueryReceived(Guid connectionId, IFramePayloadQuery payload);
    }
}
