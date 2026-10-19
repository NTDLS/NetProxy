using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service.Proxy;
using NTDLS.Persistence;
using NTDLS.ReliableMessaging;
using System.Collections.Concurrent;

namespace NetProxy.Service
{
    public class NpServiceManager
    {
        private readonly object _proxiesLock = new();
        private readonly object _saveLock = new();
        private readonly NpProxyCollection _proxies = new();
        private readonly ConcurrentDictionary<Guid, bool> _authenticatedConnections = new();
        private readonly ConcurrentDictionary<Guid, ConnectionCryptography> _connectionCryptography = new();
        private RmServer? _messageServer;

        /// <summary>
        /// The service configuration. Handlers lock it while they read or change the users or the settings.
        /// </summary>
        public NpConfiguration Configuration { get; private set; } = new() { ManagementPort = Constants.DefaultManagementPort };

        private sealed class ConnectionCryptography(ManagementCryptographyProvider provider)
        {
            public ManagementCryptographyProvider Provider { get; } = provider;
            public volatile bool IsApplied;
        }

        /// <summary>
        /// Keeps the key for a connection. It is not used until the console asks for it to be applied, because the reply to the
        /// key exchange (and the request to apply) have to be readable by the console before it has the key.
        /// </summary>
        public void InitializeCryptography(Guid connectionId, byte[] sharedSecret)
            => _connectionCryptography[connectionId] = new ConnectionCryptography(new ManagementCryptographyProvider(sharedSecret));

        public void ApplyCryptography(Guid connectionId)
        {
            if (_connectionCryptography.TryGetValue(connectionId, out var cryptography) == false)
            {
                throw new Exception("The key exchange has not been done.");
            }

            cryptography.IsApplied = true;
        }

        public bool IsEncrypted(Guid connectionId)
            => _connectionCryptography.TryGetValue(connectionId, out var cryptography) && cryptography.IsApplied;

        public bool TryGetAppliedCryptography(Guid connectionId, out ManagementCryptographyProvider provider)
        {
            if (_connectionCryptography.TryGetValue(connectionId, out var cryptography) && cryptography.IsApplied)
            {
                provider = cryptography.Provider;
                return true;
            }

            provider = null!;
            return false;
        }

        public void RemoveAuthenticated(Guid connectionId)
        {
            _authenticatedConnections.TryRemove(connectionId, out _);
            _connectionCryptography.TryRemove(connectionId, out _);
        }

        public void AddAuthenticated(Guid connectionId)
            => _authenticatedConnections[connectionId] = true;

        public bool IsAuthenticated(Guid connectionId)
            => _authenticatedConnections.ContainsKey(connectionId);

        public void Notify(Guid connectionId, IRmNotification notification)
            => _messageServer?.Notify(connectionId, notification);

        public NpProxy? GetProxyById(Guid proxyId)
        {
            lock (_proxiesLock)
            {
                return _proxies.FirstOrDefault(o => o.Configuration.Id == proxyId);
            }
        }

        public void AddProxy(NpProxy proxy)
        {
            lock (_proxiesLock)
            {
                _proxies.Add(proxy);
            }
        }

        public List<NpProxy> GetProxies()
        {
            lock (_proxiesLock)
            {
                return _proxies.ToList();
            }
        }

        public void RemoveProxy(NpProxy proxy)
        {
            lock (_proxiesLock)
            {
                _proxies.Remove(proxy);
            }
        }

        private void MessageServer_OnDisconnected(RmContext context)
        {
            RemoveAuthenticated(context.ConnectionId);
            Singletons.Logging.Write(NpLogging.Severity.Verbose,
                $"Management connection {context.ConnectionId} closed ({_authenticatedConnections.Count} logged in).");
        }

        /// <summary>
        /// Applies new service settings: the log settings take effect immediately, the rest when the service restarts.
        /// </summary>
        public void ApplySettings(NpServiceSettings settings)
        {
            lock (Configuration)
            {
                Configuration.Settings = settings;
            }

            Singletons.ConfigureLogging(settings);
            SaveConfiguration();
        }

        public void SaveConfiguration()
        {
            lock (_saveLock)
            {
                try
                {
                    //The secrets (password hashes, certificate passwords) are written encrypted. This works on copies, because
                    //  the ones in use must keep the real values.
                    NpConfiguration configuration;
                    lock (Configuration)
                    {
                        configuration = new NpConfiguration
                        {
                            ManagementPort = Configuration.ManagementPort,
                            Settings = Configuration.Settings
                        };

                        foreach (var user in Configuration.Users.Collection)
                        {
                            configuration.Users.Add(new NpUser
                            {
                                Id = user.Id,
                                UserName = user.UserName,
                                PasswordHash = SecretProtector.Protect(user.PasswordHash) ?? string.Empty,
                                Description = user.Description
                            });
                        }
                    }

                    var proxyConfigurations = GetProxies().Select(o =>
                    {
                        var copy = o.Configuration.Clone();
                        copy.TlsCertificatePassword = SecretProtector.Protect(copy.TlsCertificatePassword) ?? string.Empty;
                        return copy;
                    }).ToList();

                    CommonApplicationData.SaveToDisk(Singletons.ConfigurationName, configuration);
                    CommonApplicationData.SaveToDisk(Singletons.ConfigurationName, proxyConfigurations);
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to save configuration", ex);
                }
            }
        }

        public void LoadConfiguration()
        {
            try
            {
                var defaultConfiguration = new NpConfiguration()
                {
                    ManagementPort = Constants.DefaultManagementPort
                };
                defaultConfiguration.Users.Add(new NpUser()
                {
                    UserName = "administrator",
                    PasswordHash = NpUtility.Sha256(string.Empty)
                });

                var loaded = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, defaultConfiguration);
                var proxyConfigurations = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, new List<NpProxyConfiguration>());

                //Files from an older version (or values typed in by hand) hold plain text secrets: read them, then save them encrypted.
                bool hasPlainTextSecrets = false;

                foreach (var user in loaded.Users.Collection)
                {
                    user.PasswordHash = SecretProtector.Unprotect(user.PasswordHash, $"the password of user '{user.UserName}'", out var plain) ?? string.Empty;
                    hasPlainTextSecrets |= plain;
                }

                foreach (var proxy in proxyConfigurations)
                {
                    proxy.TlsCertificatePassword = SecretProtector.Unprotect(proxy.TlsCertificatePassword,
                        $"the certificate password of proxy '{proxy.Name}'", out var plain) ?? string.Empty;
                    hasPlainTextSecrets |= plain;
                }

                lock (Configuration)
                {
                    Configuration = loaded;
                }

                //Now that the settings are known, log the way they say to.
                Singletons.ConfigureLogging(Configuration.Settings);

                lock (_proxiesLock)
                {
                    _proxies.Clear();
                    foreach (var proxy in proxyConfigurations)
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Verbose, $"Loaded proxy '{proxy.Name}'.");
                        _proxies.Add(new NpProxy(proxy));
                    }
                }

                if (hasPlainTextSecrets)
                {
                    SaveConfiguration();
                }

                var emptyPassword = NpUtility.Sha256(string.Empty);
                foreach (var user in Configuration.Users.Collection.Where(o => o.PasswordHash.Equals(emptyPassword, StringComparison.OrdinalIgnoreCase)))
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning,
                        $"The management user '{user.UserName}' has an empty password. Set one in the NetProxy management console.");
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to load configuration", ex);
            }
        }

        public void Start()
        {
            try
            {
                LoadConfiguration();

                var server = new RmServer(new RmConfiguration
                {
                    Parameter = this,
                    ListenOnIPv6 = Configuration.Settings.ManagementListenOnIPv6
                });

                server.SetCryptographyProvider(new ServiceCryptographyProvider(this));

                server.AddHandler(new NpServiceManagerNotificationHandlers());
                server.AddHandler(new NpServiceManagerQueryHandlers());
                server.OnDisconnected += MessageServer_OnDisconnected;

                _messageServer = server;

                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"Starting the management interface on port {Configuration.ManagementPort}"
                    + (Configuration.Settings.ManagementListenOnIPv6 ? " (IPv4 and IPv6)." : " (IPv4)."));

                server.Start(Configuration.ManagementPort);

                Singletons.Logging.Write(NpLogging.Severity.Information, "Starting the proxies.");

                _proxies.Start();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to start the service", ex);
            }
        }

        public void Stop()
        {
            try
            {
                _messageServer?.Stop();
                _proxies.Stop();
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to stop the service", ex);
            }
        }
    }
}
