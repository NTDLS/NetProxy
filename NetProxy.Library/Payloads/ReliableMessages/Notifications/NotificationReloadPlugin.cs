using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    /// <summary>
    /// Unloads a plug-in and loads it again from its file, to pick up a new build of it.
    /// </summary>
    public class NotificationReloadPlugin(Guid id) : IRmNotification
    {
        public Guid Id { get; set; } = id;
    }
}
