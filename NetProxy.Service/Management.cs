using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NetProxy.Hub;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Routing;
using NetProxy.Library.Win32;
using NetProxy.Service.Routing;
using Newtonsoft.Json;

namespace NetProxy.Service
{
    public class Management
    {
        private Configuration config;
        private Packeteer packeteer = new Packeteer();
        private Routers routers = new Routers();
        private HashSet<Guid> loggedInPeers = new HashSet<Guid>();

        public Management()
        {
            packeteer.OnMessageReceived += Packeteer_OnMessageReceived;
            packeteer.OnPeerDisconnected += Packeteer_OnPeerDisconnected;
        }

        private void Packeteer_OnPeerDisconnected(Packeteer sender, NetProxy.Hub.Common.Peer peer)
        {
            lock (config)
            {
                loggedInPeers.Remove(peer.Id);
            }
            Console.WriteLine("Disconnected Session: {0} (Logged in users {1}).", peer.Id, loggedInPeers.Count());
        }

        private void Packeteer_OnMessageReceived(Packeteer sender, NetProxy.Hub.Common.Peer peer, NetProxy.Hub.Common.Packet packet)
        {
            //Console.WriteLine("{0}:{1}", packet.Label, packet.Payload);

            if (loggedInPeers.Contains(peer.Id) == false)
            {
                if (packet.Label == Constants.CommandLables.GUIRequestLogin)
                {
                    try
                    {
                        lock (config)
                        {
                            var userLogin = JsonConvert.DeserializeObject<UserLogin>(packet.Payload);

                            var singleUser = (from o in config.Users.List
                                              where o.UserName.ToLower() == userLogin.UserName.ToLower()
                                              && o.PasswordHash.ToLower() == userLogin.PasswordHash.ToLower()
                                              select o).FirstOrDefault();

                            if (singleUser != null)
                            {
                                loggedInPeers.Add(peer.Id);
                                Console.WriteLine("Logged in session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin.UserName.ToLower(), loggedInPeers.Count());
                            }
                            else
                            {
                                Console.WriteLine("Failed Login session: {0}, User: {1} (Logged in users {2}).", peer.Id, userLogin.UserName.ToLower(), loggedInPeers.Count());
                            }

                            packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(new GenericBooleanResult() { Value = singleUser != null }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                        {
                            Severity = EventLogging.Severity.Error,
                            CustomText = "An error occured while logging in.",
                            Exception = ex
                        });

                        packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                    }
                }

                return; //If the peer is not logged in, don't go any further.
            }

            if (packet.Label == Constants.CommandLables.GUIRequestRouteList)
            {
                try
                {
                    List<RouteGridItem> routes = new List<RouteGridItem>();

                    lock (config)
                    {
                        foreach (var router in routers.List)
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

                    packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(routes));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get route list.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIRequestRouteStatsList)
            {
                try
                {
                    List<RouteGridStats> stats = new List<RouteGridStats>();

                    lock (config)
                    {
                        foreach (var router in routers.List)
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

                    packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(stats));
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get route stats list.",
                        Exception = ex
                    });
                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIRequestRoute)
            {
                try
                {
                    lock (config)
                    {
                        Guid routerId = Guid.Parse(packet.Payload);
                        Router router = routers[routerId];
                        if (router != null)
                        {
                            string value = JsonConvert.SerializeObject(router.Route);
                            packeteer.SendTo(peer.Id, packet.Label, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get route.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIRequestUserList)
            {
                try
                {
                    lock (config)
                    {
                        string value = JsonConvert.SerializeObject(config.Users);
                        packeteer.SendTo(peer.Id, packet.Label, value);
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get user list.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIPersistUserList)
            {
                try
                {
                    var value = JsonConvert.DeserializeObject<Users>(packet.Payload);

                    lock (config)
                    {
                        config.Users.List.Clear();

                        foreach (var user in value.List)
                        {
                            config.Users.Add(user);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to save user list.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIPersistUpsertRoute)
            {
                try
                {
                    var value = JsonConvert.DeserializeObject<Route>(packet.Payload);

                    lock (config)
                    {
                        var existingRoute = (from o in routers.List
                                             where o.Route.Id == value.Id
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            routers.List.Remove(existingRoute);
                        }

                        var newRouter = new Router(value);

                        routers.Add(newRouter);

                        SaveConfiguration();

                        if (newRouter.Route.AutoStart)
                        {
                            newRouter.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to upsert route.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIPersistDeleteRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (config)
                    {
                        var existingRoute = (from o in routers.List
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            existingRoute.Stop();
                            routers.List.Remove(existingRoute);
                        }
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get delete route.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIPersistStopRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (config)
                    {
                        var existingRoute = (from o in routers.List
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
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to get stop route.",
                        Exception = ex
                    });

                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "Failed to stop route: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GUIPersistStartRoute)
            {
                try
                {
                    Guid routeId = Guid.Parse(packet.Payload);

                    lock (config)
                    {
                        var existingRoute = (from o in routers.List
                                             where o.Route.Id == routeId
                                             select o).FirstOrDefault();

                        if (existingRoute != null)
                        {
                            if (existingRoute.Start() == false)
                            {
                                packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "Failed to start route.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                    {
                        Severity = EventLogging.Severity.Error,
                        CustomText = "Failed to start route.",
                        Exception = ex
                    });
                    packeteer.SendTo(peer.Id, Constants.CommandLables.GUISendMessage, "Failed to start route: " + ex.Message);
                }
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string configPath = RegistryHelper.GetString(Registry.LocalMachine, Constants.RegsitryKey, "", "ConfigPath");

                string configurationText = JsonConvert.SerializeObject(config);
                File.WriteAllText(Path.Combine(configPath, Constants.SERVER_CONFIG_FILE_NAME), configurationText);

                string routesText = JsonConvert.SerializeObject(routers.Routes());
                File.WriteAllText(Path.Combine(configPath, Constants.ROUTES_CONFIG_FILE_NAME), routesText);
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
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

                string configPath = RegistryHelper.GetString(Registry.LocalMachine, Constants.RegsitryKey, "", "ConfigPath");

                Console.WriteLine("Server configuration...");
                string configurationText = File.ReadAllText(Path.Combine(configPath, Constants.SERVER_CONFIG_FILE_NAME));
                config = JsonConvert.DeserializeObject<Configuration>(configurationText);

                Console.WriteLine("Route configuration...");
                string routesText = File.ReadAllText(Path.Combine(configPath, Constants.ROUTES_CONFIG_FILE_NAME));
                List<Route> routes = JsonConvert.DeserializeObject<List<Route>>(routesText);

                if (routes != null)
                {
                    foreach (var route in routes)
                    {
                        Console.WriteLine("Adding route {0}.", route.Name);
                        routers.Add(new Router(route));
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
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
                    TrafficType = TrafficType.HTTP,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = false,
                    AutoStart = true,
                    Description = "Default example route."
               
                };

                route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new Endpoint("www.NetworkDLS.com", 80));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HTTPVerb.Any, "Host", HttpHeaderAction.Upsert, "www.NetworkDLS.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                Route route = new Route()
                {
                    Name = "Ingenuity",
                    ListenPort = 81,
                    TrafficType = TrafficType.HTTP,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new Endpoint("www.IngenuitySC.com", 80));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HTTPVerb.Any, "Host", HttpHeaderAction.Upsert, "www.IngenuitySC.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------
            {
                Route route = new Route()
                {
                    Name = "Microsoft LIVE!",
                    ListenPort = 82,
                    TrafficType = TrafficType.HTTPS,
                    MaxBufferSize = 1021 * 1024,
                    ListenOnAllAddresses = true,
                    AutoStart = true
                };

                //route.Bindings.Add(new Binding { Enabled = true, Address = "127.0.0.1" });

                route.Endpoints.ConnectionPattern = ConnectionPattern.FailOver;
                route.Endpoints.Add(new Endpoint("login.live.com", 443));

                route.HttpHeaderRules.Add(new HttpHeaderRule(HttpHeaderType.Request, HTTPVerb.Any, "Host", HttpHeaderAction.Upsert, "login.live.com"));

                routers.Add(new Router(route));
            }
            //------------------------------------------------------------------------------------------------------------------

            string routesText = JsonConvert.SerializeObject(routers.Routes());

            string configPath = RegistryHelper.GetString(Registry.LocalMachine, Constants.RegsitryKey, "", "ConfigPath");
            File.WriteAllText(Path.Combine(configPath, Constants.ROUTES_CONFIG_FILE_NAME), routesText);
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();

                Console.WriteLine("Starting management interface on port {0}.", config.ManagementPort);
                packeteer.Start(config.ManagementPort);

                Console.WriteLine("starting routes...");
                routers.Start();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
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

                packeteer.Stop();
                routers.Stop();
            }
            catch (Exception ex)
            {
                Singletons.EventLog.WriteEvent(new EventLogging.EventPayload
                {
                    Severity = EventLogging.Severity.Error,
                    CustomText = "Failed to stop router.",
                    Exception = ex
                });
            }
        }
    }
}
