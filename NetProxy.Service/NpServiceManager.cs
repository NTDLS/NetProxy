using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Proxy;
using NTDLS.NullExtensions;
using NTDLS.Persistence;
using NTDLS.ReliableMessaging;

namespace NetProxy.Service
{
    public class NpServiceManager
    {
        private NpConfiguration? _config;
        private readonly RmServer _messageServer = new();
        private readonly NpProxyCollection _proxies = new();
        private readonly HashSet<Guid> _authenticatedConnections = new();

        public NpServiceManager()
        {
            _messageServer.OnNotificationReceived += _messageServer_OnNotificationReceived;
            _messageServer.OnQueryReceived += _messageServer_OnQueryReceived;
            _messageServer.OnDisconnected += _messageServer_OnDisconnected;
        }

        private IRmQueryReply _messageServer_OnQueryReceived(RmContext context, IRmPayload payload)
        {
            if (_authenticatedConnections.Contains(context.ConnectionId) == false)
            {
                var reply = new QueryLoginReply();

                if (payload is QueryLogin userLogin)
                {
                    try
                    {
                        lock (_config.EnsureNotNull())
                        {
                            if (_config.Users.Collection.Where(o =>
                                o.UserName.ToLower() == userLogin.UserName.ToLower() && o.PasswordHash.ToLower() == userLogin.PasswordHash.ToLower()).Any())
                            {
                                _authenticatedConnections.Add(context.ConnectionId);
                                Console.WriteLine($"Logged in connection: {context.ConnectionId}, User: {userLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                            else
                            {
                                Console.WriteLine($"Failed login connection: {context.ConnectionId}, User: {userLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
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
                else
                {
                    throw new Exception("Unhandled pre-login query.");
                }
            }

            if (payload is QueryProxyConfigurationList)
            {
                var reply = new QueryProxyConfigurationListReply();

                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        foreach (var proxy in _proxies)
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
            else if (payload is QueryProxyStatistics)
            {
                var reply = new QueryProxyStatisticsReply();

                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        foreach (var proxy in _proxies)
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
            else if (payload is QueryProxyConfiguration proxyRequest)
            {
                var reply = new QueryProxyConfigurationReply();

                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        var proxy = _proxies[proxyRequest.Id];
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
            else if (payload is QueryUserList)
            {
                var reply = new QueryUserListReply();

                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        reply.Collection = _config.Users.Collection;
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to get user list.", ex);
                    reply.Message = ex.Message;
                }

                return reply;
            }

            throw new Exception("Unhandled query.");
        }

        private void _messageServer_OnDisconnected(RmContext context)
        {
            lock (_config.EnsureNotNull())
            {
                _authenticatedConnections.Remove(context.ConnectionId);
            }
            Console.WriteLine($"Deregistered connection: {context.ConnectionId} (Logged in users {_authenticatedConnections.Count}).");
        }

        private void _messageServer_OnNotificationReceived(RmContext context, IRmNotification payload)
        {
            if (_authenticatedConnections.Contains(context.ConnectionId) == false)
            {
                if (payload is NotificationRegisterLogin registerLogin)
                {
                    try
                    {
                        lock (_config.EnsureNotNull())
                        {
                            if (_config.Users.Collection.Where(o =>
                                o.UserName.ToLower() == registerLogin.UserName.ToLower() && o.PasswordHash.ToLower() == registerLogin.PasswordHash.ToLower()).Any())
                            {
                                _authenticatedConnections.Add(context.ConnectionId);
                                Console.WriteLine($"Registered connection: {context.ConnectionId}, User: {registerLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to register connection: {context.ConnectionId}, User: {registerLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.Logging.Write("An error occurred while logging in.", ex);
                    }
                }
                else
                {
                    throw new Exception("Unhandled pre-login notification.");
                }

                return; //If the peer is not logged in, don't go any further.
            }

            if (payload is NotificationPersistUserList persistUserList)
            {
                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        _config.Users.Collection.Clear();

                        foreach (var user in persistUserList.Collection)
                        {
                            _config.Users.Add(user);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to save user list.", ex);
                }
            }
            else if (payload is NotificationUpsertProxy persistUpsertProxy)
            {
                try
                {

                    lock (_config.EnsureNotNull())
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == persistUpsertProxy.ProxyConfiguration.Id
                                             select o).FirstOrDefault();

                        if (existingProxy != null)
                        {
                            existingProxy.Stop();
                            _proxies.Remove(existingProxy);
                        }

                        var newProxy = new NpProxy(persistUpsertProxy.ProxyConfiguration);

                        _proxies.Add(newProxy);

                        SaveConfiguration();

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
            else if (payload is NotificationDeleteProxy persistDeleteProxy)
            {
                try
                {

                    lock (_config.EnsureNotNull())
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == persistDeleteProxy.Id
                                             select o).FirstOrDefault();

                        if (existingProxy != null)
                        {
                            existingProxy.Stop();
                            _proxies.Remove(existingProxy);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to delete proxy.", ex);
                }
            }
            else if (payload is NotificationStopProxy persistStopProxy)
            {
                try
                {
                    lock (_config.EnsureNotNull())
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == persistStopProxy.Id
                                             select o).FirstOrDefault();

                        existingProxy?.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to stop proxy.", ex);
                }
            }
            else if (payload is NotificationStartProxy persistStartProxy)
            {
                try
                {

                    lock (_config.EnsureNotNull())
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == persistStartProxy.Id
                                             select o).FirstOrDefault();

                        if (existingProxy?.Start() != true)
                        {
                            _messageServer.Notify(context.ConnectionId, new NotificationMessage("Failed to start proxy."));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to start proxy.", ex);
                }
            }
            else
            {
                throw new Exception("Unhandled notification.");
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                CommonApplicationData.SaveToDisk(Constants.FriendlyName, _config);
                CommonApplicationData.SaveToDisk(Constants.FriendlyName, _proxies.CloneConfigurations());
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to save configuration.",
                    Exception = ex
                });
            }
        }

        public void LoadConfiguration()
        {
            try
            {
                Console.WriteLine("Loading configuration...");

                //AddTestProxys();

                var defaultConfiguration = new NpConfiguration()
                {
                    ManagementPort = 5854
                };
                defaultConfiguration.Users.Add(new NpUser()
                {
                    UserName = "administrator",
                    PasswordHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                });

                Console.WriteLine("Server configuration...");
                _config = CommonApplicationData.LoadFromDisk(Constants.FriendlyName, defaultConfiguration);

                Console.WriteLine("Proxy configuration...");
                var proxies = CommonApplicationData.LoadFromDisk(Constants.FriendlyName, new List<NpProxyConfiguration>());
                foreach (var proxy in proxies)
                {
                    Console.WriteLine("Adding proxy {0}.", proxy.Name);
                    _proxies.Add(new NpProxy(proxy));
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to load configuration.",
                    Exception = ex
                });
            }
        }

        private void AddTestProxys()
        {
            NpProxyCollection proxies = new();

            //------------------------------------------------------------------------------------------------------------------
            {
                NpProxyConfiguration proxy = new NpProxyConfiguration()
                {
                    Name = "NetworkDLS",
                    ListenPort = 80,
                    TrafficType = TrafficType.Http,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = false,
                    AutoStart = true,
                    Description = "Default example proxy."

                };

                proxy.Bindings.Add(new NpBinding { Enabled = true, Address = "127.0.0.1" });

                proxy.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                proxy.Endpoints.Add(new NpEndpoint("www.NetworkDLS.com", 80));

                proxy.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.NetworkDLS.com"));

                proxies.Add(new NpProxy(proxy));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                NpProxyConfiguration proxy = new NpProxyConfiguration()
                {
                    Name = "Ingenuity",
                    ListenPort = 81,
                    TrafficType = TrafficType.Http,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //proxy.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                proxy.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                proxy.Endpoints.Add(new NpEndpoint("www.IngenuitySC.com", 80));

                proxy.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.IngenuitySC.com"));

                proxies.Add(new NpProxy(proxy));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                NpProxyConfiguration proxy = new NpProxyConfiguration()
                {
                    Name = "Microsoft LIVE!",
                    ListenPort = 82,
                    TrafficType = TrafficType.Https,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //proxy.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                proxy.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                proxy.Endpoints.Add(new NpEndpoint("login.live.com", 443));

                proxy.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "login.live.com"));

                proxies.Add(new NpProxy(proxy));
            }
            //------------------------------------------------------------------------------------------------------------------

            CommonApplicationData.SaveToDisk(Constants.FriendlyName, proxies.CloneConfigurations());
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();

                Console.WriteLine("Starting management interface on port {0}.", _config.EnsureNotNull().ManagementPort);
                _messageServer.Start(_config.ManagementPort);

                Console.WriteLine("starting proxies...");
                _proxies.Start();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to start proxy.",
                    Exception = ex
                });
            }
        }

        public void Stop()
        {
            try
            {
                SaveConfiguration();

                _messageServer.Stop();
                _proxies.Stop();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to stop proxy.",
                    Exception = ex
                });
            }
        }
    }
}
