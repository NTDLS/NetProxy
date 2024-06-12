using NetProxy.Library.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.MessageHubPayloads.Notifications
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
