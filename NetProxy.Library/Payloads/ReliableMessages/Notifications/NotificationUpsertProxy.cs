using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Notifications
{
    public class NotificationUpsertProxy(NpProxyConfiguration proxyConfiguration) : IRmNotification
    {
        public NpProxyConfiguration ProxyConfiguration { get; set; } = proxyConfiguration;
    }
}
