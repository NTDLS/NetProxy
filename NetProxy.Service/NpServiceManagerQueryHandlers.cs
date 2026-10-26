using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.ReliableMessaging;
using NTDLS.SecureKeyExchange;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Service
{
    internal class NpServiceManagerQueryHandlers : NpServiceManagerMessageHandlerBase, IRmMessageHandler
    {
        /// <summary>
        /// A console has connected and starts the key exchange. The key is kept, but not applied until the console says it is
        /// ready for it (<see cref="OnQueryApplyCryptography"/>), because this reply has to be readable without it.
        /// </summary>
        public QueryRequestKeyExchangeReply OnQueryRequestKeyExchange(RmContext context, QueryRequestKeyExchange query)
        {
            var serviceManager = GetServiceManager(context);

            var compoundNegotiator = new CompoundNegotiator();
            var replyToken = compoundNegotiator.ApplyNegotiationToken(query.NegotiationToken);

            serviceManager.InitializeCryptography(context.ConnectionId, compoundNegotiator.SharedSecret);

            return new QueryRequestKeyExchangeReply(replyToken);
        }

        /// <summary>
        /// The console is switching to encryption: everything it sends after this request is encrypted, and so is the reply to it.
        /// </summary>
        public QueryApplyCryptographyReply OnQueryApplyCryptography(RmContext context, QueryApplyCryptography query)
        {
            GetServiceManager(context).ApplyCryptography(context.ConnectionId);
            return new QueryApplyCryptographyReply();
        }

        public QueryLoginReply OnQueryLogin(RmContext context, QueryLogin query)
        {
            var serviceManager = GetServiceManager(context);

            var reply = new QueryLoginReply();

            try
            {
                //Nobody logs in (and sends a password hash) over a connection that is not encrypted.
                if (serviceManager.IsEncrypted(context.ConnectionId) == false)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning,
                        $"A login was refused because connection {context.ConnectionId} is not encrypted. The console is probably an older version.");
                    reply.Message = "The connection is not encrypted. Update the console to the same version as the service.";
                    return reply;
                }

                bool isValid;

                lock (serviceManager.Configuration)
                {
                    isValid = serviceManager.Configuration.Users.Collection.Any(o =>
                        o.UserName.Equals(query.UserName, StringComparison.CurrentCultureIgnoreCase)
                        && AreHashesEqual(o.PasswordHash, query.PasswordHash));
                }

                Singletons.Plugins.ForService().ManagementLoginAsync(new NetProxy.PluginApi.NpLoginInfo(query.UserName, isValid)).AsTask().GetAwaiter().GetResult();

                if (isValid)
                {
                    serviceManager.AddAuthenticated(context.ConnectionId);
                    Singletons.Logging.Write(NpLogging.Severity.Information, $"User '{query.UserName}' logged in (connection {context.ConnectionId}).");
                    reply.Result = true;
                }
                else
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning, $"Failed login for user '{query.UserName}' (connection {context.ConnectionId}).");

                    //Makes guessing passwords slow.
                    Thread.Sleep(500);
                    reply.Result = false;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("An error occurred while logging in", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        /// <summary>
        /// Compares password hashes in constant time, so that the time taken does not tell how much of a guess was right.
        /// </summary>
        private static bool AreHashesEqual(string? expected, string? actual)
        {
            var a = Encoding.UTF8.GetBytes((expected ?? string.Empty).ToLowerInvariant());
            var b = Encoding.UTF8.GetBytes((actual ?? string.Empty).ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        public QueryProxyConfigurationListReply OnQueryProxyConfigurationList(RmContext context, QueryProxyConfigurationList query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyConfigurationListReply();

            try
            {
                foreach (var proxy in serviceManager.GetProxies())
                {
                    //Only what the list shows. The rest (such as the certificate password) is not sent.
                    reply.Collection.Add(new NpProxyGridItem()
                    {
                        Id = proxy.Configuration.Id,
                        Name = proxy.Configuration.Name,
                        TrafficType = proxy.Configuration.TrafficType,
                        ProxyType = proxy.Configuration.TrafficType.ToString() + " / " + (proxy.Configuration.ListenOnAllAddresses
                            ? proxy.Configuration.BindingProtocol switch { BindingProtocol.IPv4 => "IPv4", BindingProtocol.IPv6 => "IPv6", _ => "IPv4+IPv6" }
                            : "Bindings"),
                        BindingProtocol = proxy.Configuration.BindingProtocol,
                        Description = proxy.Configuration.Description,
                        IsRunning = proxy.IsRunning,
                        ListenPort = proxy.Configuration.ListenPort,
                        ListenOnAllAddresses = proxy.Configuration.ListenOnAllAddresses,
                        Bindings = proxy.Configuration.Bindings,
                        HostNames = proxy.Configuration.HostNames.Select(o => new NpHostName { Enabled = o.Enabled, Host = o.Host }).ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the proxy list", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryPluginListReply OnQueryPluginList(RmContext context, QueryPluginList query)
        {
            EnforceLoginAndGetServiceManager(context);

            var reply = new QueryPluginListReply();

            try
            {
                reply.Collection = Singletons.Plugins.GetGridItems();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the plugin list", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryServiceInfoReply OnQueryServiceInfo(RmContext context, QueryServiceInfo query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var proxies = serviceManager.GetProxies();

            return new QueryServiceInfoReply
            {
                Version = typeof(NpServiceManager).Assembly.GetName().Version?.ToString(3) ?? string.Empty,
                UptimeSeconds = (long)(DateTime.UtcNow - serviceManager.StartedUtc).TotalSeconds,
                ProxyCount = proxies.Count,
                RunningProxyCount = proxies.Count(o => o.IsRunning),
                PluginCount = Singletons.Plugins.GetGridItems().Count,
                ManagementPort = serviceManager.Configuration.ManagementPort,
                ConfigurationName = Singletons.ConfigurationName,
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription
            };
        }

        public QueryUpsertProxyReply OnQueryUpsertProxy(RmContext context, QueryUpsertProxy query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryUpsertProxyReply();

            try
            {
                var problem = serviceManager.UpsertProxy(query.Proxy, query.MatchByName, out var action, out var startError);
                reply.Success = problem == null;
                reply.Message = problem;
                reply.Action = action;
                reply.Id = query.Proxy.Id;
                reply.StartError = startError;

                if (problem == null)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Information, $"Proxy '{query.Proxy.Name}' was {action} by connection {context.ConnectionId}.");
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the proxy", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryActionReply OnQueryProxyAction(RmContext context, QueryProxyAction query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryActionReply();

            try
            {
                var problem = serviceManager.ApplyProxyAction(query.Id, query.Name, query.Action);
                reply.Success = problem == null;
                reply.Message = problem;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write($"Failed to {query.Action} the proxy", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryValidateProxyReply OnQueryValidateProxy(RmContext context, QueryValidateProxy query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryValidateProxyReply();

            try
            {
                reply.Problems = serviceManager.ValidateProxy(query.Proxy, query.MatchByName);
            }
            catch (Exception ex)
            {
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryCloseConnectionsReply OnQueryCloseConnections(RmContext context, QueryCloseConnections query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryCloseConnectionsReply();

            try
            {
                reply.Closed = serviceManager.CloseConnections(query.ConnectionId, query.ProxyId, query.ProxyName);
            }
            catch (Exception ex)
            {
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryActionReply OnQueryUserAction(RmContext context, QueryUserAction query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryActionReply();

            try
            {
                var problem = serviceManager.ApplyUserAction(query.Action, query.UserName, query.PasswordHash, query.Description);
                reply.Success = problem == null;
                reply.Message = problem;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to change the users", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryReloadConfigurationReply OnQueryReloadConfiguration(RmContext context, QueryReloadConfiguration query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryReloadConfigurationReply();

            try
            {
                var result = serviceManager.ReloadConfiguration(query.DryRun);
                reply.Success = result.Success;
                reply.Changes = result.Changes;
                reply.Problems = result.Problems;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to reload the configuration", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryLogTailReply OnQueryLogTail(RmContext context, QueryLogTail query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryLogTailReply();

            reply.Lines = serviceManager.TailLog(query.Lines, out var message);
            reply.Message = message;
            return reply;
        }

        public QueryBrowseReply OnQueryBrowse(RmContext context, QueryBrowse query)
        {
            EnforceLoginAndGetServiceManager(context);
            return NpFileBrowser.Browse(query, AppContext.BaseDirectory);
        }

        public QueryExportConfigurationReply OnQueryExportConfiguration(RmContext context, QueryExportConfiguration query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryExportConfigurationReply();

            try
            {
                reply.Document = serviceManager.ExportConfiguration();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to export the configuration", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryImportConfigurationReply OnQueryImportConfiguration(RmContext context, QueryImportConfiguration query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            try
            {
                var reply = serviceManager.ImportConfiguration(query.Document, query.DryRun);
                if (query.DryRun == false && reply.Success)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Information, $"The configuration was replaced by an import from connection {context.ConnectionId}.");
                }

                return reply;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to import the configuration", ex);
                return new QueryImportConfigurationReply { Message = ex.GetBaseException().Message };
            }
        }

        public QueryCertificateListReply OnQueryCertificateList(RmContext context, QueryCertificateList query)
        {
            EnforceLoginAndGetServiceManager(context);
            var reply = new QueryCertificateListReply();

            try
            {
                reply.Collection = Singletons.Certificates.GetGridItems();
            }
            catch (Exception ex)
            {
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryUpsertCertificateReply OnQueryUpsertCertificate(RmContext context, QueryUpsertCertificate query)
        {
            EnforceLoginAndGetServiceManager(context);
            var reply = new QueryUpsertCertificateReply();

            try
            {
                var certificate = query.Certificate;

                if (query.MatchByName)
                {
                    var existing = Singletons.Certificates.GetGridItems()
                        .FirstOrDefault(o => o.Name.Equals(certificate.Name?.Trim(), StringComparison.CurrentCultureIgnoreCase));
                    if (existing != null)
                    {
                        certificate.Id = existing.Id;
                    }
                }

                var problem = Singletons.Certificates.Upsert(certificate, out var loadError, out var created);
                reply.Success = problem == null;
                reply.Message = problem;
                reply.Action = created ? "created" : "updated";
                reply.LoadError = loadError;

                if (problem == null)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Certificate '{certificate.Name}' was saved by connection {context.ConnectionId}.");
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the certificate", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryActionReply OnQueryCertificateAction(RmContext context, QueryCertificateAction query)
        {
            EnforceLoginAndGetServiceManager(context);
            var reply = new QueryActionReply();

            try
            {
                var item = Singletons.Certificates.GetGridItems().FirstOrDefault(o => query.Id != null
                    ? o.Id == query.Id
                    : o.Name.Equals(query.Name?.Trim(), StringComparison.CurrentCultureIgnoreCase));

                if (item == null)
                {
                    reply.Message = "There is no such certificate.";
                    return reply;
                }

                var problem = Singletons.Certificates.Delete(item.Id, out _);
                reply.Success = problem == null;
                reply.Message = problem;

                if (problem == null)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Certificate '{item.Name}' was deleted by connection {context.ConnectionId}.");
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to delete the certificate", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryUpsertPluginReply OnQueryUpsertPlugin(RmContext context, QueryUpsertPlugin query)
        {
            EnforceLoginAndGetServiceManager(context);
            var reply = new QueryUpsertPluginReply();

            try
            {
                var existing = query.MatchByName
                    ? Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Name.Equals(query.Plugin.Name.Trim(), StringComparison.CurrentCultureIgnoreCase))
                    : Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == query.Plugin.Id);

                if (existing != null)
                {
                    query.Plugin.Id = existing.Id;
                }

                var problem = Singletons.Plugins.UpsertAsync(query.Plugin).GetAwaiter().GetResult();
                reply.Success = problem == null;
                reply.Message = problem;
                reply.Action = existing != null ? "updated" : "created";
                reply.LoadError = Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == query.Plugin.Id)?.LoadError;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the plugin", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryActionReply OnQueryPluginAction(RmContext context, QueryPluginAction query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);
            var reply = new QueryActionReply();

            try
            {
                var plugin = Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == query.Id
                    || (string.IsNullOrWhiteSpace(query.Name) == false && o.Name.Equals(query.Name.Trim(), StringComparison.CurrentCultureIgnoreCase)));

                if (plugin == null)
                {
                    reply.Message = $"There is no plugin '{query.Name ?? query.Id?.ToString()}'.";
                    return reply;
                }

                switch (query.Action)
                {
                    case NpPluginAction.Delete:
                        reply.Success = Singletons.Plugins.DeleteAsync(plugin.Id).GetAwaiter().GetResult();
                        serviceManager.RemovePluginFromProxies(plugin.Id);
                        break;

                    case NpPluginAction.Reload:
                        reply.Success = Singletons.Plugins.ReloadAsync(plugin.Id).GetAwaiter().GetResult();
                        reply.Message = Singletons.Plugins.GetGridItems().FirstOrDefault(o => o.Id == plugin.Id)?.LoadError;
                        break;

                    default:
                        Singletons.Plugins.MoveAsync(plugin.Id, query.Action == NpPluginAction.MoveUp ? -1 : 1).GetAwaiter().GetResult();
                        reply.Success = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to change the plugin", ex);
                reply.Message = ex.GetBaseException().Message;
            }

            return reply;
        }

        public QueryProxyStatisticsReply OnQueryProxyStatistics(RmContext context, QueryProxyStatistics query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyStatisticsReply();

            try
            {
                foreach (var proxy in serviceManager.GetProxies())
                {
                    reply.Collection.Add(new NpProxyGridStats()
                    {
                        Id = proxy.Configuration.Id,
                        IsRunning = proxy.IsRunning,
                        BytesRead = proxy.Statistics.BytesRead,
                        BytesWritten = proxy.Statistics.BytesWritten,
                        TotalConnections = proxy.Statistics.TotalConnections,
                        CurrentConnections = proxy.Statistics.CurrentConnections
                    });
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the proxy statistics", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryProxyConnectionsReply OnQueryProxyConnections(RmContext context, QueryProxyConnections query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyConnectionsReply();

            try
            {
                var proxy = serviceManager.GetProxyById(query.ProxyId);
                if (proxy != null)
                {
                    reply.Collection = proxy.ActiveConnections.Values.Select(o => o.GetGridItem()).OrderBy(o => o.StartDateTime).ToList();
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the proxy connections", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryProxyConfigurationReply OnQueryProxyConfiguration(RmContext context, QueryProxyConfiguration query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyConfigurationReply();

            try
            {
                var proxy = serviceManager.GetProxyById(query.Id);
                if (proxy != null)
                {
                    reply.ProxyConfiguration = proxy.Configuration;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the proxy", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryUserListReply OnQueryUserList(RmContext context, QueryUserList query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryUserListReply();

            try
            {
                lock (serviceManager.Configuration)
                {
                    //The password hashes stay on the server. A user that is saved with no password hash keeps the one it has.
                    reply.Collection = serviceManager.Configuration.Users.Collection.Select(o => new NpUser
                    {
                        Id = o.Id,
                        UserName = o.UserName,
                        PasswordHash = string.Empty,
                        Description = o.Description
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the user list", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryServiceSettingsReply OnQueryServiceSettings(RmContext context, QueryServiceSettings query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryServiceSettingsReply();

            try
            {
                lock (serviceManager.Configuration)
                {
                    reply.Settings = serviceManager.Configuration.Settings;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get the service settings", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }
    }
}
