using NetProxy.Library;
using NetProxy.Library.MessageHubPayloads.Notifications;
using NetProxy.Library.MessageHubPayloads.Queries;
using NetProxy.Library.Payloads;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Proxy;
using NTDLS.Persistence;
using NTDLS.ReliableMessaging;
using NTDLS.StreamFraming.Payloads;

namespace NetProxy.Service
{
    public class NpServiceManager
    {
        private NpConfiguration? _config;
        private readonly MessageServer _messageServer = new();
        private readonly NpProxyCollection _proxies = new();
        private readonly HashSet<Guid> _authenticatedConnections = new();

        public NpServiceManager()
        {
            _messageServer.OnNotificationReceived += _MessageHubServer_OnNotificationReceived;
            _messageServer.OnQueryReceived += _messageServer_OnQueryReceived;
            _messageServer.OnDisconnected += _MessageHubServer_OnDisconnected;
        }

        private IFrameQueryReply _messageServer_OnQueryReceived(Guid connectionId, IFrameQuery payload)
        {
            NpUtility.EnsureNotNull(_config);

            if (_authenticatedConnections.Contains(connectionId) == false)
            {
                var reply = new QueryLoginReply();

                if (payload is QueryLogin userLogin)
                {
                    try
                    {
                        lock (_config)
                        {
                            if (_config.Users.Collection.Where(o =>
                                o.UserName.ToLower() == userLogin.UserName.ToLower() && o.PasswordHash.ToLower() == userLogin.PasswordHash.ToLower()).Any())
                            {
                                _authenticatedConnections.Add(connectionId);
                                Console.WriteLine($"Logged in connection: {connectionId}, User: {userLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                            else
                            {
                                Console.WriteLine($"Failed login connection: {connectionId}, User: {userLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }

                            reply.Result = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.Logging.Write("An error occured while logging in.", ex);
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
                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            var augmentedProxy = new NpProxyGridItem()
                            {
                                Id = proxy.Configuration.Id,
                                Name = proxy.Configuration.Name,
                                TrafficType = proxy.Configuration.TrafficType,
                                ProxyType = proxy.Configuration.TrafficType.ToString() + " / " + proxy.Configuration.BindingProtocal.ToString(),
                                BindingProtocal = proxy.Configuration.BindingProtocal,
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
            else if (payload is QueryProxyStatsistics)
            {
                var reply = new QueryProxyStatsisticsReply();

                try
                {
                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            var augmentedProxy = new NpProxyGridStats()
                            {
                                Id = proxy.Configuration.Id,
                                IsRunning = proxy.IsRunning,
                                BytesReceived = proxy.Statistics.BytesReceived,
                                BytesSent = proxy.Statistics.BytesSent,
                                TotalConnections = proxy.Statistics.TotalConnections,
                                CurrentConnections = proxy.CurrentConnectionCount

                            };
                            reply.Collection.Add(augmentedProxy);
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
                    lock (_config)
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
                    lock (_config)
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

        private void _MessageHubServer_OnDisconnected(Guid connectionId)
        {
            NpUtility.EnsureNotNull(_config);

            lock (_config)
            {
                _authenticatedConnections.Remove(connectionId);
            }
            Console.WriteLine($"Deregistered connection: {connectionId} (Logged in users {_authenticatedConnections.Count}).");
        }

        private void _MessageHubServer_OnNotificationReceived(Guid connectionId, IFrameNotification payload)
        {
            NpUtility.EnsureNotNull(_config);

            if (_authenticatedConnections.Contains(connectionId) == false)
            {
                if (payload is NotifificationRegisterLogin registerLogin)
                {
                    try
                    {
                        lock (_config)
                        {
                            if (_config.Users.Collection.Where(o =>
                                o.UserName.ToLower() == registerLogin.UserName.ToLower() && o.PasswordHash.ToLower() == registerLogin.PasswordHash.ToLower()).Any())
                            {
                                _authenticatedConnections.Add(connectionId);
                                Console.WriteLine($"Registered connection: {connectionId}, User: {registerLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to register connection: {connectionId}, User: {registerLogin.UserName} (Logged in users {_authenticatedConnections.Count}).");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.Logging.Write("An error occured while logging in.", ex);
                    }
                }
                else
                {
                    throw new Exception("Unhandled pre-login notification.");
                }

                return; //If the peer is not logged in, don't go any further.
            }

            if (payload is NotifificationPersistUserList persistUserList)
            {
                try
                {
                    lock (_config)
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
            else if (payload is NotifificationUpsertProxy persistUpsertProxy)
            {
                try
                {

                    lock (_config)
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
            else if (payload is NotifificationDeleteProxy persistDeleteProxy)
            {
                try
                {

                    lock (_config)
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
            else if (payload is NotifificationStopProxy persistStopProxy)
            {
                try
                {
                    lock (_config)
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
            else if (payload is NotifificationStartProxy persistStartProxy)
            {
                try
                {

                    lock (_config)
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == persistStartProxy.Id
                                             select o).FirstOrDefault();

                        if (existingProxy?.Start() != true)
                        {
                            _messageServer.SendNotification(connectionId, new NotifificationMessage("Failed to start proxy."));
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
                CommonApplicationData.SaveToDisk(Constants.TitleCaption, _config);
                CommonApplicationData.SaveToDisk(Constants.TitleCaption, _proxies.CloneConfigurations());
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
                defaultConfiguration.Users.Add(new NpUser() { UserName = "administrator", PasswordHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" });

                Console.WriteLine("Server configuration...");
                _config = CommonApplicationData.LoadFromDisk<NpConfiguration>(Constants.TitleCaption, defaultConfiguration);

                Console.WriteLine("Proxy configuration...");
                var proxys = CommonApplicationData.LoadFromDisk<List<NpProxyConfiguration>>(Constants.TitleCaption, new List<NpProxyConfiguration>());
                foreach (var proxy in proxys)
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

            CommonApplicationData.SaveToDisk(Constants.TitleCaption, proxies.CloneConfigurations());
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();
                NpUtility.EnsureNotNull(_config);

                Console.WriteLine("Starting management interface on port {0}.", _config.ManagementPort);
                _messageServer.Start(_config.ManagementPort);

                Console.WriteLine("starting proxys...");
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
