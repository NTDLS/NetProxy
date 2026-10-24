using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
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
                var problem = serviceManager.UpsertProxy(notification.ProxyConfiguration, matchByName: false, out _, out var startError);

                if (problem != null)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The proxy was not saved. {problem}"));
                    return;
                }

                if (startError != null)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The proxy was saved but could not be started: {startError}"));
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

                    existingProxy.Plugins.ProxyConfigurationChangedAsync(existingProxy.Info, NpChangeKind.Deleted).AsTask().GetAwaiter().GetResult();
                }

                serviceManager.SaveConfiguration();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to delete the proxy", ex);
            }
        }

        public void OnNotificationUpsertPlugin(RmContext context, NotificationUpsertPlugin notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var problem = Singletons.Plugins.UpsertAsync(notification.Plugin).GetAwaiter().GetResult();
                if (problem != null)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The plugin was not saved. {problem}"));
                    return;
                }

                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"Plugin '{notification.Plugin.Name}' was saved by connection {context.ConnectionId}.");

                var loadError = Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == notification.Plugin.Id)?.LoadError;
                if (string.IsNullOrEmpty(loadError) == false)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The plugin was saved but could not be loaded: {loadError}"));
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the plugin", ex);
                serviceManager.Notify(context.ConnectionId, new NotificationMessage($"Failed to save the plugin: {ex.Message}"));
            }
        }

        public void OnNotificationDeletePlugin(RmContext context, NotificationDeletePlugin notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                if (Singletons.Plugins.DeleteAsync(notification.Id).GetAwaiter().GetResult())
                {
                    serviceManager.RemovePluginFromProxies(notification.Id);
                    Singletons.Logging.Write(NpLogging.Severity.Information, $"A plugin was deleted by connection {context.ConnectionId}.");
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to delete the plugin", ex);
            }
        }

        public void OnNotificationMovePlugin(RmContext context, NotificationMovePlugin notification)
        {
            EnforceLoginAndGetServiceManager(context);

            try
            {
                Singletons.Plugins.MoveAsync(notification.Id, notification.Direction).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to move the plugin", ex);
            }
        }

        public void OnNotificationReloadPlugin(RmContext context, NotificationReloadPlugin notification)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                Singletons.Plugins.ReloadAsync(notification.Id).GetAwaiter().GetResult();

                var loadError = Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == notification.Id)?.LoadError;
                if (string.IsNullOrEmpty(loadError) == false)
                {
                    serviceManager.Notify(context.ConnectionId, new NotificationMessage($"The plugin could not be loaded: {loadError}"));
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to reload the plugin", ex);
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
