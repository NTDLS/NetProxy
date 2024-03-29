﻿using NetProxy.Library.Routing;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Library.MessageHubPayloads.Notifications
{
    public class NotifificationUpsertProxy : IFrameNotification
    {
        public NpProxyConfiguration ProxyConfiguration { get; set; }

        public NotifificationUpsertProxy(NpProxyConfiguration proxyConfiguration)
        {
            ProxyConfiguration = proxyConfiguration;
        }
    }
}
