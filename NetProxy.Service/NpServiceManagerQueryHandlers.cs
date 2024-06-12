using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Utilities;
using NTDLS.NullExtensions;
using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    internal class NpServiceManagerQueryHandlers : NpServiceManagerMessageHandlerBase, IRmMessageHandler
    {
        public QueryLoginReply OnQueryLogin(RmContext context, QueryLogin query)
        {
            var serviceManager = (context.Endpoint.Parameter as NpServiceManager).EnsureNotNull();

            var reply = new QueryLoginReply();

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    if (serviceManager.Configuration.Users.Collection.Where(o =>
                        o.UserName.Equals(query.UserName, StringComparison.CurrentCultureIgnoreCase)
                        && o.PasswordHash.Equals(query.PasswordHash, StringComparison.CurrentCultureIgnoreCase)).Any())
                    {
                        serviceManager.AddAuthenticated(context.ConnectionId);
                        Singletons.Logging.Write(NpLogging.Severity.Verbose,
                            $"Logged in connection: {context.ConnectionId}, User: {query.UserName}.");
                    }
                    else
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Verbose,
                            $"Failed login connection: {context.ConnectionId}, User: {query.UserName}.");
                    }

                    reply.Result = true;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("An error occurred while logging in.", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryProxyConfigurationListReply OnQueryProxyConfigurationList(RmContext context, QueryProxyConfigurationList query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyConfigurationListReply();

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    foreach (var proxy in serviceManager.GetProxies())
                    {
                        var augmentedProxy = new NpProxyGridItem()
                        {
                            Id = proxy.Configuration.Id,
                            Name = proxy.Configuration.Name,
                            TrafficType = proxy.Configuration.TrafficType,
                            ProxyType = proxy.Configuration.TrafficType.ToString() + " / " + proxy.Configuration.BindingProtocol.ToString(),
                            BindingProtocol = proxy.Configuration.BindingProtocol,
                            Description = proxy.Configuration.Description,
                            IsRunning = proxy.IsRunning,
                            ListenPort = proxy.Configuration.ListenPort,
                            ListenOnAllAddresses = proxy.Configuration.ListenOnAllAddresses,
                            Bindings = proxy.Configuration.Bindings
                        };

                        reply.Collection.Add(augmentedProxy);
                    }
                }

                return reply;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get proxy list.", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }

        public QueryProxyStatisticsReply OnQueryProxyStatistics(RmContext context, QueryProxyStatistics query)
        {
            var serviceManager = EnforceLoginAndGetServiceManager(context);

            var reply = new QueryProxyStatisticsReply();

            try
            {
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    foreach (var proxy in serviceManager.GetProxies())
                    {
                        proxy.Statistics.Use((o) =>
                        {
                            var statistics = new NpProxyGridStats()
                            {
                                Id = proxy.Configuration.Id,
                                IsRunning = proxy.IsRunning,
                                BytesRead = o.BytesRead,
                                BytesWritten = o.BytesWritten,
                                TotalConnections = o.TotalConnections,
                                CurrentConnections = o.CurrentConnections

                            };
                            reply.Collection.Add(statistics);
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get proxy stats list.", ex);
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
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    var proxy = serviceManager.GetProxyById(query.Id);
                    if (proxy != null)
                    {
                        reply.ProxyConfiguration = proxy.Configuration;
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get proxy.", ex);
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
                lock (serviceManager.Configuration.EnsureNotNull())
                {
                    reply.Collection = serviceManager.Configuration.Users.Collection;
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to get user list.", ex);
                reply.Message = ex.Message;
            }

            return reply;
        }
    }
}
