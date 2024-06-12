using NetProxy.Library;
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
        public NpConfiguration? Configuration { get; set; }
        private readonly RmServer _messageServer;
        private readonly NpProxyCollection _proxies = new();
        public HashSet<Guid> AuthenticatedConnections { get; set; } = new();

        public NpServiceManager()
        {
            _messageServer = new RmServer(new RmConfiguration
            {
                Parameter = this
            });

            _messageServer.AddHandler(new NpServiceManagerNotificationHandlers());
            _messageServer.AddHandler(new NpServiceManagerQueryHandlers());

            _messageServer.OnDisconnected += _messageServer_OnDisconnected;
        }

        public void Notify(Guid connectionId, IRmNotification notification)
            => _messageServer.Notify(connectionId, notification);


        public NpProxy? GetProxyById(Guid proxyId)
            => _proxies.Where(o => o.Configuration.Id == proxyId).SingleOrDefault();

        public void AddProxy(NpProxy proxy)
            => _proxies.Add(proxy);

        public List<NpProxy> GetProxies()
            => _proxies.ToList();

        public void RemoveProxy(NpProxy proxy)
            => _proxies.Remove(proxy);

        private void _messageServer_OnDisconnected(RmContext context)
        {
            lock (Configuration.EnsureNotNull())
            {
                AuthenticatedConnections.Remove(context.ConnectionId);
            }
            Console.WriteLine($"Deregistered connection: {context.ConnectionId} (Logged in users {AuthenticatedConnections.Count}).");
        }

        public void SaveConfiguration()
        {
            try
            {
                CommonApplicationData.SaveToDisk(Constants.FriendlyName, Configuration);
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
                Configuration = CommonApplicationData.LoadFromDisk(Constants.FriendlyName, defaultConfiguration);

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

                Console.WriteLine("Starting management interface on port {0}.", Configuration.EnsureNotNull().ManagementPort);
                _messageServer.Start(Configuration.ManagementPort);

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
