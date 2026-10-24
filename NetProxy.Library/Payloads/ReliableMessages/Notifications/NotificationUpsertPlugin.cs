using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    /// <summary>
    /// Adds a plug-in (at the end of the list), or changes it. The service (re)loads it.
    /// </summary>
    public class NotificationUpsertPlugin(NpPluginConfiguration plugin) : IRmNotification
    {
        public NpPluginConfiguration Plugin { get; set; } = plugin;
    }
}
