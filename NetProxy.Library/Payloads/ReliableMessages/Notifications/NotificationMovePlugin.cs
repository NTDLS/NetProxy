using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    /// <summary>
    /// Moves a plug-in up (-1) or down (1) in the order that the plug-ins are called in.
    /// </summary>
    public class NotificationMovePlugin(Guid id, int direction) : IRmNotification
    {
        public Guid Id { get; set; } = id;
        public int Direction { get; set; } = direction;
    }
}
