using NetProxy.Hub;
using NetProxy.Hub.MessageFraming;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Proxy;
using Newtonsoft.Json;
using NTDLS.Persistence;

namespace NetProxy.Service
{
    public class NpManagement
    {
        private NpConfiguration? _config;
        private readonly NpHubPacketeer _packeteer = new();
        private readonly NpProxyCollection _proxies = new();
        private readonly HashSet<Guid> _loggedInPeers = new();

        public NpManagement()
        {
            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
            _packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;
        }

        private void Packeteer_OnPeerDisconnected(NpHubPacketeer sender, NetProxy.Hub.Common.NpHubPeer peer)
        {
            NpUtility.EnsureNotNull(_config);

            lock (_config)
            {
                _loggedInPeers.Remove(peer.Id);
            }
            Console.WriteLine("Disconnected Session: {0} (Logged in users {1}).", peer.Id, _loggedInPeers.Count());
        }

        private void Packeteer_OnMessageReceived(NpHubPacketeer sender, NetProxy.Hub.Common.NpHubPeer peer, NpFrame packet)
        {
            NpUtility.EnsureNotNull(_config);

            if (_loggedInPeers.Contains(peer.Id) == false)
            {
                if (packet.Label == Constants.CommandLables.GuiRequestLogin)
                {
                    try
                    {
                        lock (_config)
                        {
                            var userLogin = JsonConvert.DeserializeObject<NpUserLogin>(packet.Payload);

                            var singleUser = (from o in _config.Users.Collection
                                              where o.UserName.ToLower() == userLogin?.UserName?.ToLower()
                                              && o.PasswordHash.ToLower() == userLogin.PasswordHash.ToLower()
                                              select o).FirstOrDefault();

                            if (singleUser != null)
                            {
                                _loggedInPeers.Add(peer.Id);
                                Console.WriteLine("Logged in session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin?.UserName?.ToLower(), _loggedInPeers.Count());
                            }
                            else
                            {
                                Console.WriteLine("Failed Login session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin?.UserName?.ToLower(), _loggedInPeers.Count());
                            }

                            _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(new NpGenericBooleanResult() { Value = singleUser != null }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                        {
                            Severity = NpLogging.Severity.Exception,
                            CustomText = "An error occured while logging in.",
                            Exception = ex
                        });

                        _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                    }
                }

                return; //If the peer is not logged in, don't go any further.
            }

            if (packet.Label == Constants.CommandLables.GuiRequestRouteList)
            {
                try
                {
                    List<NpRouteGridItem> routes = new();

                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            NpRouteGridItem augmentedRoute = new()
                            {
                                Id = proxy.Route.Id,
                                Name = proxy.Route.Name,
                                TrafficType = proxy.Route.TrafficType,
                                ProxyType = proxy.Route.TrafficType.ToString() + " / " + proxy.Route.BindingProtocal.ToString(),
                                BindingProtocal = proxy.Route.BindingProtocal,
                                Description = proxy.Route.Description,
                                IsRunning = proxy.IsRunning,
                                ListenPort = proxy.Route.ListenPort,
                                ListenOnAllAddresses = proxy.Route.ListenOnAllAddresses,
                                Bindings = proxy.Route.Bindings
                            };

                            routes.Add(augmentedRoute);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(routes));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get route list.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiRequestRouteStatsList)
            {
                try
                {
                    List<NpRouteGridStats> stats = new List<NpRouteGridStats>();

                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            NpRouteGridStats augmentedRoute = new NpRouteGridStats()
                            {
                                Id = proxy.Route.Id,
                                IsRunning = proxy.IsRunning,
                                BytesReceived = proxy.Statistics.BytesReceived,
                                BytesSent = proxy.Statistics.BytesSent,
                                TotalConnections = proxy.Statistics.TotalConnections,
                                CurrentConnections = proxy.CurrentConnectionCount

                            };
                            stats.Add(augmentedRoute);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(stats));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get route stats list.",
                        Exception = ex
                    });
                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiRequestRoute)
            {
                try
                {
                    lock (_config)
                    {
                        Guid proxyId = Guid.Parse(packet.Payload);
                        var proxy = _proxies[proxyId];
                        if (proxy != null)
                        {
                            string value = JsonConvert.SerializeObject(proxy.Route);
                            _packeteer.SendTo(peer.Id, packet.Label, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get route.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiRequestUserList)
            {
                try
                {
                    lock (_config)
                    {
                        string value = JsonConvert.SerializeObject(_config.Users);
                        _packeteer.SendTo(peer.Id, packet.Label, value);
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get user list.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistUserList)
            {
                try
                {
                    var value = JsonConvert.DeserializeObject<NpUsers>(packet.Payload);
                    if (value != null)
                    {
                        lock (_config)
                        {
                            _config.Users.Collection.Clear();

                            foreach (var user in value.Collection)
                            {
                                _config.Users.Add(user);
                            }
                            SaveConfiguration();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to save user list.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistUpsertRoute)
            {
                try
                {
                    var value = JsonConvert.DeserializeObject<NpRoute>(packet.Payload);
                    NpUtility.EnsureNotNull(value);

                    lock (_config)
                    {
                        var existingRoute = (from o in _proxies
                                             where o.Route.Id == value.Id
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            _proxies.Remove(existingRoute);
                        }

                        var newProxy = new NpProxy(value);

                        _proxies.Add(newProxy);

                        SaveConfiguration();

                        if (newProxy.Route.AutoStart)
                        {
                            newProxy.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to upsert route.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistDeleteRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingRoute = (from o in _proxies
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            _proxies.Remove(existingRoute);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get delete route.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistStopRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingRoute = (from o in _proxies
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get stop route.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to stop route: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistStartRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingRoute = (from o in _proxies
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            if (existingRoute.Start() == false)
                            {
                                _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to start route.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to start route.",
                        Exception = ex
                    });
                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to start route: " + ex.Message);
                }
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                CommonApplicationData.SaveToDisk(Constants.TitleCaption, _config);
                CommonApplicationData.SaveToDisk(Constants.TitleCaption, _proxies.Routes());
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
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

                //AddTestRoutes();

                var defaultConfiguration = new NpConfiguration()
                {
                    ManagementPort = 5854
                };
                defaultConfiguration.Users.Add(new NpUser() { UserName = "administrator", PasswordHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" });

                Console.WriteLine("Server configuration...");
                _config = CommonApplicationData.LoadFromDisk<NpConfiguration>(Constants.TitleCaption, defaultConfiguration);

                Console.WriteLine("Route configuration...");
                var routes = CommonApplicationData.LoadFromDisk<List<NpRoute>>(Constants.TitleCaption, new List<NpRoute>());
                foreach (var route in routes)
                {
                    Console.WriteLine("Adding route {0}.", route.Name);
                    _proxies.Add(new NpProxy(route));
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to load configuration.",
                    Exception = ex
                });
            }
        }

        private void AddTestRoutes()
        {
            NpProxyCollection proxies = new();

            //------------------------------------------------------------------------------------------------------------------
            {
                NpRoute route = new NpRoute()
                {
                    Name = "NetworkDLS",
                    ListenPort = 80,
                    TrafficType = TrafficType.Http,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = false,
                    AutoStart = true,
                    Description = "Default example route."

                };

                route.Bindings.Add(new NpBinding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new NpEndpoint("www.NetworkDLS.com", 80));

                route.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.NetworkDLS.com"));

                proxies.Add(new NpProxy(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                NpRoute route = new NpRoute()
                {
                    Name = "Ingenuity",
                    ListenPort = 81,
                    TrafficType = TrafficType.Http,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new NpEndpoint("www.IngenuitySC.com", 80));

                route.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.IngenuitySC.com"));

                proxies.Add(new NpProxy(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                NpRoute route = new NpRoute()
                {
                    Name = "Microsoft LIVE!",
                    ListenPort = 82,
                    TrafficType = TrafficType.Https,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new NpEndpoint("login.live.com", 443));

                route.HttpHeaderRules.Add(new NpHttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "login.live.com"));

                proxies.Add(new NpProxy(route));
            }
            //------------------------------------------------------------------------------------------------------------------

            CommonApplicationData.SaveToDisk(Constants.TitleCaption, proxies.Routes());
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();
                NpUtility.EnsureNotNull(_config);

                Console.WriteLine("Starting management interface on port {0}.", _config.ManagementPort);
                _packeteer.Start(_config.ManagementPort);

                Console.WriteLine("starting routes...");
                _proxies.Start();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
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

                _packeteer.Stop();
                _proxies.Stop();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteLog(new NpLogging.LoggingPayload
                {
                    Severity = NpLogging.Severity.Exception,
                    CustomText = "Failed to stop proxy.",
                    Exception = ex
                });
            }
        }
    }
}
