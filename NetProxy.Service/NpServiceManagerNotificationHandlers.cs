using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Proxy;
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
                lock (serviceManager.Configuration)
                {
                    var existingUsers = serviceManager.Configuration.Users.Collection.ToList();
                    var users = new List<NpUser>();

                    foreach (var user in notification.Collection)
                    {
                        if (string.IsNullOrWhiteSpace(user.UserName))
                        {
                            continue;
                        }

                        var existing = existingUsers.FirstOrDefault(o => o.UserName.Equals(user.UserName, StringComparison.CurrentCultureIgnoreCase));

                        if (string.IsNullOrEmpty(user.PasswordHash))
                        {
                            //The console only sends a password hash for a user whose password was set. Everyone else keeps theirs.
                            if (existing == null)
                            {
                                Singletons.Logging.Write(NpLogging.Severity.Warning,
                                    $"The user '{user.UserName}' was not added because it has no password.");
                                continue;
                            }

                            user.PasswordHash = existing.PasswordHash;
                        }

                        users.Add(user);
                    }

                    if (users.Count == 0)
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Warning,
                            "The user list was not saved because it has no users, which would lock everyone out.");
                        return;
                    }

                    serviceManager.Configuration.Users.Collection.Clear();
                    foreach (var user in users)
                    {
                        serviceManager.Configuration.Users.Add(user);
                    }
                }

                serviceManager.SaveConfiguration();
                Singletons.Logging.Write(NpLogging.Severity.Information, $"The user list was saved by connection {context.ConnectionId}.");
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the user list", ex);
            }
        }

        public void OnNotificationPersistServiceSettings(RmContext context, NotificationPersistServiceSettings notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var settings = notification.Settings;

                //Keep the values where the log file sink can work with them.
                settings.LogFileMaxSizeMB = Math.Clamp(settings.LogFileMaxSizeMB, 1, 10240);
                settings.LogFileRetainedCount = Math.Clamp(settings.LogFileRetainedCount, 0, 1000);
                settings.LogPath = (settings.LogPath ?? string.Empty).Trim();

                serviceManager.ApplySettings(settings);
                Singletons.Logging.Write(NpLogging.Severity.Information, $"The service settings were saved by connection {context.ConnectionId}.");
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the service settings", ex);
                serviceManager.Notify(context.ConnectionId, new NotificationMessage($"Failed to save the service settings: {ex.Message}"));
            }
        }

        public void OnNotificationUpsertProxy(RmContext context, NotificationUpsertProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var configuration = notification.ProxyConfiguration;

                var problem = NpHostRouter.NormalizeAndValidate(configuration)
                    ?? NpHostRouter.FindSharingConflict(configuration, serviceManager.GetProxies().Select(o => o.Configuration));

                if (problem != null)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The proxy was not saved. {problem}"));
                    return;
                }

                var existingProxy = serviceManager.GetProxyById(notification.ProxyConfiguration.Id);

                if (existingProxy != null)
                {
                    existingProxy.Stop();
                    serviceManager.RemoveProxy(existingProxy);
                }

                var newProxy = new NpProxy(notification.ProxyConfiguration);

                serviceManager.AddProxy(newProxy);
                serviceManager.SaveConfiguration();

                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"Proxy '{newProxy.Configuration.Name}' was {(existingProxy != null ? "changed" : "added")} by connection {context.ConnectionId}.");

                if (newProxy.Configuration.AutoStart && newProxy.Start(out var error) == false)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The proxy was saved but could not be started: {error}"));
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the proxy", ex);
                serviceManager.Notify(context.ConnectionId, new NotificationMessage($"Failed to save the proxy: {ex.Message}"));
            }
        }

        public void OnNotificationDeleteProxy(RmContext context, NotificationDeleteProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var existingProxy = serviceManager.GetProxyById(notification.Id);

                if (existingProxy != null)
                {
                    existingProxy.Stop();
                    serviceManager.RemoveProxy(existingProxy);
                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Proxy '{existingProxy.Configuration.Name}' was deleted by connection {context.ConnectionId}.");
                }

                serviceManager.SaveConfiguration();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to delete the proxy", ex);
            }
        }

        public void OnNotificationStopProxy(RmContext context, NotificationStopProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                serviceManager.GetProxyById(notification.Id)?.Stop();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to stop the proxy", ex);
            }
        }

        public void OnNotificationStartProxy(RmContext context, NotificationStartProxy notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var existingProxy = serviceManager.GetProxyById(notification.Id);

                if (existingProxy != null && existingProxy.Start(out var error) == false)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The proxy could not be started: {error}"));
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to start the proxy", ex);
            }
        }
    }
}
