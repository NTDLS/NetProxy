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
                        Singletons.Logging.Write(new NpLogging.LoggingPayload
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

            if (packet.Label == Constants.CommandLables.GuiRequestProxyList)
            {
                try
                {
                    List<NpProxyGridItem> gridItems = new();

                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            NpProxyGridItem augmentedProxy = new()
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

                            gridItems.Add(augmentedProxy);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(gridItems));
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get proxy list.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiRequestProxyStatsList)
            {
                try
                {
                    List<NpProxyGridStats> stats = new List<NpProxyGridStats>();

                    lock (_config)
                    {
                        foreach (var proxy in _proxies)
                        {
                            NpProxyGridStats augmentedProxy = new NpProxyGridStats()
                            {
                                Id = proxy.Configuration.Id,
                                IsRunning = proxy.IsRunning,
                                BytesReceived = proxy.Statistics.BytesReceived,
                                BytesSent = proxy.Statistics.BytesSent,
                                TotalConnections = proxy.Statistics.TotalConnections,
                                CurrentConnections = proxy.CurrentConnectionCount

                            };
                            stats.Add(augmentedProxy);
                        }
                    }

                    _packeteer.SendTo(peer.Id, packet.Label, JsonConvert.SerializeObject(stats));
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get proxy stats list.",
                        Exception = ex
                    });
                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiRequestProxy)
            {
                try
                {
                    lock (_config)
                    {
                        Guid proxyId = Guid.Parse(packet.Payload);
                        var proxy = _proxies[proxyId];
                        if (proxy != null)
                        {
                            string value = JsonConvert.SerializeObject(proxy.Configuration);
                            _packeteer.SendTo(peer.Id, packet.Label, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get proxy.",
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
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
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
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to save user list.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistUpsertProxy)
            {
                try
                {
                    var value = JsonConvert.DeserializeObject<NpProxyConfiguration>(packet.Payload);
                    NpUtility.EnsureNotNull(value);

                    lock (_config)
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == value.Id
                                             select o).FirstOrDefault();

                        if (existingProxy != null)
                        {
                            existingProxy.Stop();
                            _proxies.Remove(existingProxy);
                        }

                        var newProxy = new NpProxy(value);

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
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to upsert proxy.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistDeleteProxy)
            {
                try
                {
                    Guid proxyId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == proxyId
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
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get delete proxy.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "The operation failed: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistStopProxy)
            {
                try
                {
                    Guid proxyId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == proxyId
                                             select o).FirstOrDefault();

                        if (existingProxy != null)
                        {
                            existingProxy.Stop();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to get stop proxy.",
                        Exception = ex
                    });

                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to stop proxy: " + ex.Message);
                }
            }
            else if (packet.Label == Constants.CommandLables.GuiPersistStartProxy)
            {
                try
                {
                    Guid proxyId = Guid.Parse(packet.Payload);

                    lock (_config)
                    {
                        var existingProxy = (from o in _proxies
                                             where o.Configuration.Id == proxyId
                                             select o).FirstOrDefault();

                        if (existingProxy != null)
                        {
                            if (existingProxy.Start() == false)
                            {
                                _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to start proxy.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(new NpLogging.LoggingPayload
                    {
                        Severity = NpLogging.Severity.Exception,
                        CustomText = "Failed to start proxy.",
                        Exception = ex
                    });
                    _packeteer.SendTo(peer.Id, Constants.CommandLables.GuiSendMessage, "Failed to start proxy: " + ex.Message);
                }
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
                _packeteer.Start(_config.ManagementPort);

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

                _packeteer.Stop();
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
