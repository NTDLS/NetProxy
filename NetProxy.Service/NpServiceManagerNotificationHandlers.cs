using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Service.Proxy;
using NTDLS.Helpers;
using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    internal class NpServiceManagerNotificationHandlers : NpServiceManagerMessageHandlerBase, IRmMessageHandler
    {
        public void OnNotificationPersistUserList(RmContext context, NotificationPersistUserList notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    serviceManager.Configuration.Users.Collection.Clear();

                    foreach (var user in notification.Collection)
                    {
                        serviceManager.Configuration.Users.Add(user);
                    }
                    serviceManager.SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save user list.", ex);
            }

        }

        public void OnNotificationUpsertProxy(RmContext context, NotificationUpsertProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    var existingProxy = serviceManager.GetProxyById(notification.ProxyConfiguration.Id);

                    if (existingProxy != null)
                    {
                        existingProxy.Stop();
                        serviceManager.RemoveProxy(existingProxy);
                    }

                    var newProxy = new NpProxy(notification.ProxyConfiguration);

                    serviceManager.AddProxy(newProxy);

                    serviceManager.SaveConfiguration();

                    if (newProxy.Configuration.AutoStart)
                    {
                        newProxy.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to upsert proxy.", ex);
            }

        }

        public void OnNotificationDeleteProxy(RmContext context, NotificationDeleteProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {

                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    var existingProxy = serviceManager.GetProxyById(notification.Id);

                    if (existingProxy != null)
                    {
                        existingProxy.Stop();
                        serviceManager.RemoveProxy(existingProxy);
                    }
                    serviceManager.SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to delete proxy.", ex);
            }
        }

        public void OnNotificationStopProxy(RmContext context, NotificationStopProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    var existingProxy = serviceManager.GetProxyById(notification.Id);

                    existingProxy?.Stop();
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to stop proxy.", ex);
            }
        }

        public void OnNotificationStartProxy(RmContext context, NotificationStartProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    var existingProxy = serviceManager.GetProxyById(notification.Id);

                    if (existingProxy?.Start() != true)
                    {
                        serviceManager.Notify(context.ConnectionId, new NotificationMessage("Failed to start proxy."));
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to start proxy.", ex);
            }
        }
    }
}
