using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Routing;
using NetProxy.Library.Utility;
using NetProxy.Service.Routing;
using Newtonsoft.Json;
using NTDLS.Persistence;

namespace NetProxy.Service
{
    public class Management
    {
        private Configuration _config;
        private readonly Packeteer _packeteer = new();
        private readonly Routers _routers = new();
        private readonly HashSet<Guid> _loggedInPeers = new();

        public Management()
        {
            _packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
            _packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;
        }

        private void Packeteer_OnPeerDisconnected(Packeteer sender, NetProxy.Hub.Common.Peer peer)
        {
            lock (_config)
            {
                _loggedInPeers.Remove(peer.Id);
            }
            Console.WriteLine("Disconnected Session: {0} (Logged in users {1}).", peer.Id, _loggedInPeers.Count());
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            Console.WriteLine("{0}:{1}", packet.Label, packet.Payload);

            if (_loggedInPeers.Contains(peer.Id) == false)
            {
                if (packet.Label == Constants.CommandLables.GuiRequestLogin)
                {
                    try
                    {
                        lock (_config)
                        {
                            var userLogin = JsonConvert.DeserializeObject<UserLogin>(packet.Payload);

                            var singleUser = (from o in _config.Users.List
                                              where o.UserName.ToLower() == userLogin.UserName.ToLower()
                                              && o.PasswordHash.ToLower() == userLogin.PasswordHash.ToLower()
                                              select o).FirstOrDefault();

                            if (singleUser != null)
                            {
                                _loggedInPeers.Add(peer.Id);
                                Console.WriteLine("Logged in session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin.UserName.ToLower(), _loggedInPeers.Count());
                            }
                            else
                            {
                                Console.WriteLine("Failed Login session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin.UserName.ToLower(), _loggedInPeers.Count());
                            }

                            _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(new GenericBooleanResult() { Value = singleUser != null }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.EventLog.WriteEvent(new Logging.EventPayload
                        {
                            Severity = Logging.Severity.Error,
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
                    List<RouteGridItem> routes = new List<RouteGridItem>();

                    lock (_config)
                    {
                        foreach (var router in _routers.List)
                        {
                            RouteGridItem augmentedRoute = new RouteGridItem()
                            {
                                Id = router.Route.Id,
                                Name = router.Route.Name,
                                TrafficType = router.Route.TrafficType,
                                RouterType = router.Route.TrafficType.ToString() + " / " + router.Route.BindingProtocal.ToString(),
                                BindingProtocal = router.Route.BindingProtocal,
                                Description = router.Route.Description,
                                IsRunning = router.IsRunning,
                                ListenPort = router.Route.ListenPort,
                                ListenOnAllAddresses = router.Route.ListenOnAllAddresses,
                                Bindings = router.Route.Bindings
                            };

                            routes.Add(augmentedRoute);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(routes));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                    List<RouteGridStats> stats = new List<RouteGridStats>();

                    lock (_config)
                    {
                        foreach (var router in _routers.List)
                        {
                            RouteGridStats augmentedRoute = new RouteGridStats()
                            {
                                Id = router.Route.Id,
                                IsRunning = router.IsRunning,
                                BytesReceived = router.Stats.BytesReceived,
                                BytesSent = router.Stats.BytesSent,
                                TotalConnections = router.Stats.TotalConnections,
                                CurrentConnections = router.CurrentConnectionCount

                            };
                            stats.Add(augmentedRoute);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(stats));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                        Guid routerId = Guid.Parse(packet.Payload);
                        Router router = _routers[routerId];
                        if (router != null)
                        {
                            string value = JsonConvert.SerializeObject(router.Route);
                            _packeteer.SendTo(peer.Id, packet.Label, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                    var value = JsonConvert.DeserializeObject<Users>(packet.Payload);

                    lock (_config)
                    {
                        _config.Users.List.Clear();

                        foreach (var user in value.List)
                        {
                            _config.Users.Add(user);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                    var value = JsonConvert.DeserializeObject<Route>(packet.Payload);

                    lock (_config)
                    {
                        var existingRoute = (from o in _routers.List
                                             where o.Route.Id == value.Id
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            _routers.List.Remove(existingRoute);
                        }

                        var newRouter = new Router(value);

                        _routers.Add(newRouter);

                        SaveConfiguration();

                        if (newRouter.Route.AutoStart)
                        {
                            newRouter.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                        var existingRoute = (from o in _routers.List
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            _routers.List.Remove(existingRoute);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                        var existingRoute = (from o in _routers.List
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
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                        var existingRoute = (from o in _routers.List
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
                    Singletons.EventLog.WriteEvent(new Logging.EventPayload
                    {
                        Severity = Logging.Severity.Error,
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
                CommonApplicationData.SaveToDisk("NetProxy.Service", _config);
                CommonApplicationData.SaveToDisk("NetProxy.Service", _routers.Routes());
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new Logging.EventPayload
                {
                    Severity = Logging.Severity.Error,
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

                var defaultConfiguration = new Configuration()
                {
                    ManagementPort = 5854
                };
                defaultConfiguration.Users.Add(new User() { UserName = "administrator", PasswordHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" });

                Console.WriteLine("Server configuration...");
                _config = CommonApplicationData.LoadFromDisk<Configuration>("NetworkDLS NetProxy", defaultConfiguration);

                Console.WriteLine("Route configuration...");
                var routes = CommonApplicationData.LoadFromDisk<List<Route>>("NetworkDLS NetProxy", new List<Route>());
                foreach (var route in routes)
                {
                    Console.WriteLine("Adding route {0}.", route.Name);
                    _routers.Add(new Router(route));
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new Logging.EventPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to load configuration.",
                    Exception = ex
                });
            }
        }

        private void AddTestRoutes()
        {
            Routers routers = new Routers();

            //------------------------------------------------------------------------------------------------------------------
            {
                Route route = new Route()
                {
                    Name = "NetworkDLS",
                    ListenPort = 80,
                    TrafficType = TrafficType.Http,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = false,
                    AutoStart = true,
                    Description = "Default example route."

                };

                route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new Endpoint("www.NetworkDLS.com", 80));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.NetworkDLS.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                Route route = new Route()
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
                route.Endpoints.Add(new Endpoint("www.IngenuitySC.com", 80));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "www.IngenuitySC.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                Route route = new Route()
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
                route.Endpoints.Add(new Endpoint("login.live.com", 443));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HttpVerb.Any, "Host", HttpHeaderAction.Upsert, "login.live.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------

            CommonApplicationData.SaveToDisk("NetProxy.Service", routers.Routes());
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();

                Console.WriteLine("Starting management interface on port {0}.", _config.ManagementPort);
                _packeteer.Start(_config.ManagementPort);

                Console.WriteLine("starting routes...");
                _routers.Start();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new Logging.EventPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to start router.",
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
                _routers.Stop();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new Logging.EventPayload
                {
                    Severity = Logging.Severity.Error,
                    CustomText = "Failed to stop router.",
                    Exception = ex
                });
            }
        }
    }
}
