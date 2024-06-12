using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationUpsertProxy : IRmNotification
    {
        public NpProxyConfiguration ProxyConfiguration { get; set; }

        public NotificationUpsertProxy(NpProxyConfiguration proxyConfiguration)
        {
            ProxyConfiguration = proxyConfiguration;
        }
    }
}
