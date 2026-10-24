using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Notifications;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using Newtonsoft.Json;
using NTDLS.ReliableMessaging;
using NTDLS.SecureKeyExchange;

namespace NetProxy.ClientApi
{
    /// <summary>
    /// Where to connect, and who to log in as. The password is sent as its hash, which is also what a credentials file can keep.
    /// </summary>
    public class NpConnectionOptions
    {
        public string Server { get; set; } = "localhost";
        public int Port { get; set; } = Constants.DefaultManagementPort;
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public static string HashPassword(string password) => NpUtility.Sha256(password);
    }

    /// <summary>
    /// What happened when a proxy was applied.
    /// </summary>
    public class NpApplyResult
    {
        /// <summary>"create", "update" or "unchanged" (these are what would happen, for a dry run), or "created" / "updated".</summary>
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; }
        public List<string> Problems { get; set; } = new();
        public string? StartError { get; set; }
    }

    /// <summary>
    /// Everything that the service has, in one document: what the command line tool exports and imports.
    /// </summary>
    public class NpExport
    {
        public int FormatVersion { get; set; } = 1;
        public List<NpProxyConfiguration> Proxies { get; set; } = new();
        public List<NpPluginConfiguration> Plugins { get; set; } = new();
        public NpServiceSettings? Settings { get; set; }
    }

    /// <summary>
    /// A connection to a NetProxy service that is logged in, with the operations of the console as methods. The operations that change
    /// something say how it went, so they can be used from scripts.
    /// </summary>
    public sealed class NpServiceClient : IDisposable
    {
        /// <summary>
        /// What secrets are shown as. A configuration that is applied with this in place of a secret keeps the secret that it has.
        /// </summary>
        public const string SecretMask = "********";

        private readonly RmClient _client;

        public RmClient Raw => _client;

        private NpServiceClient(RmClient client)
        {
            _client = client;
        }

        public void Dispose()
        {
            try { _client.Disconnect(); } catch { }
        }

        //---------------------------------------------------------------- Connecting

        /// <summary>
        /// Connects to the service, encrypts the connection, and logs in. Throws (with a message to show) if it cannot.
        /// </summary>
        public static NpServiceClient Connect(NpConnectionOptions options)
            => new(ConnectRaw(options.Server, options.Port, options.UserName, options.PasswordHash));

        /// <summary>
        /// The login sequence: connect, agree on a key (so the password hash is never sent in the clear), switch to encryption, log in.
        /// The connection is closed if any of it fails.
        /// </summary>
        public static RmClient ConnectRaw(string server, int port, string userName, string passwordHash, string? badLoginMessage = null)
        {
            var client = new RmClient();

            try
            {
                client.Connect(server, port);

                var compoundNegotiator = new CompoundNegotiator();
                var negotiationToken = compoundNegotiator.GenerateNegotiationToken((int)Math.Ceiling(Constants.ManagementCryptographyKeySize / 128.0));

                //The first thing we do with a new connection is start a key exchange.
                var keyExchangeReply = client.Query(new QueryRequestKeyExchange(negotiationToken));
                compoundNegotiator.ApplyNegotiationResponseToken(keyExchangeReply.NegotiationToken);

                var cryptographyProvider = new ManagementCryptographyProvider(compoundNegotiator.SharedSecret);

                //Tell the service that we are switching to encryption.
                client.Query(new QueryApplyCryptography(), () =>
                {
                    //The request is built (unencrypted) by now: everything after it, including its reply, is encrypted.
                    client.SetCryptographyProvider(cryptographyProvider);
                });

                var loginResult = client.Query(new QueryLogin(userName, passwordHash));
                if (loginResult.Result != true)
                {
                    throw new Exception(string.IsNullOrEmpty(loginResult.Message) ? badLoginMessage ?? "The user name or password is not correct." : loginResult.Message);
                }

                return client;
            }
            catch
            {
                try { client.Disconnect(); } catch { }
                throw;
            }
        }

        //---------------------------------------------------------------- Service

        public QueryServiceInfoReply GetInfo() => _client.Query<QueryServiceInfoReply>(new QueryServiceInfo());

        /// <summary>
        /// Re-reads the configuration files and applies what changed. See <see cref="QueryReloadConfiguration"/>.
        /// </summary>
        public QueryReloadConfigurationReply Reload(bool dryRun = false)
            => _client.Query<QueryReloadConfigurationReply>(new QueryReloadConfiguration { DryRun = dryRun });

        public List<string> GetLog(int lines, out string? message)
        {
            var reply = _client.Query<QueryLogTailReply>(new QueryLogTail { Lines = lines });
            message = reply.Message;
            return reply.Lines;
        }

        public List<NpCertificateStatus> GetCertificates()
            => _client.Query<QueryCertificateStatusReply>(new QueryCertificateStatus()).Collection;

        /// <summary>
        /// Lists a folder on the machine that the service runs on. See <see cref="QueryBrowse"/>.
        /// </summary>
        public QueryBrowseReply Browse(string path, bool foldersOnly = false, params string[] extensions)
            => _client.Query<QueryBrowseReply>(new QueryBrowse { Path = path, FoldersOnly = foldersOnly, Extensions = extensions.ToList() });

        public NpServiceSettings GetSettings() => _client.Query<QueryServiceSettingsReply>(new QueryServiceSettings()).Settings;

        /// <summary>
        /// Saves the settings and returns the settings that the service has afterwards.
        /// </summary>
        public NpServiceSettings SetSettings(NpServiceSettings settings)
        {
            _client.Notify(new NotificationPersistServiceSettings(settings));
            Thread.Sleep(400);
            return GetSettings();
        }

        //---------------------------------------------------------------- Proxies

        public List<NpProxyGridItem> ListProxies()
            => _client.Query<QueryProxyConfigurationListReply>(new QueryProxyConfigurationList()).Collection;

        public Dictionary<Guid, NpProxyGridStats> GetStatistics()
            => _client.Query<QueryProxyStatisticsReply>(new QueryProxyStatistics()).Collection.ToDictionary(o => o.Id);

        /// <summary>
        /// The full configuration of the proxy with this name (not case sensitive), or null.
        /// </summary>
        public NpProxyConfiguration? GetProxy(string name)
        {
            var item = ListProxies().FirstOrDefault(o => o.Name.Equals(name.Trim(), StringComparison.CurrentCultureIgnoreCase));
            return item == null ? null : _client.Query<QueryProxyConfigurationReply>(new QueryProxyConfiguration(item.Id)).ProxyConfiguration;
        }

        public List<NpProxyConfiguration> GetAllProxies()
            => ListProxies().Select(o => _client.Query<QueryProxyConfigurationReply>(new QueryProxyConfiguration(o.Id)).ProxyConfiguration).ToList();

        /// <summary>
        /// Adds the proxy, or replaces the one with the same name. A proxy that is the same as the one the service has is left
        /// alone (it is not restarted). A secret that is <see cref="SecretMask"/> keeps the secret of the proxy it replaces.
        /// With <paramref name="dryRun"/> it only checks, and says what would happen.
        /// </summary>
        public NpApplyResult ApplyProxy(NpProxyConfiguration configuration, bool dryRun = false)
        {
            var result = new NpApplyResult();
            var existing = string.IsNullOrWhiteSpace(configuration.Name) ? null : GetProxy(configuration.Name);

            RestoreSecrets(configuration, existing);

            if (existing != null)
            {
                configuration.AdoptEndpointIds(existing);
            }

            var validation = _client.Query<QueryValidateProxyReply>(new QueryValidateProxy { Proxy = configuration, MatchByName = true });
            result.Problems = validation.Problems;
            if (validation.Message != null)
            {
                result.Problems.Add(validation.Message);
            }

            if (existing != null && AreSame(existing, configuration) && result.Problems.Count == 0)
            {
                result.Action = "unchanged";
                result.Success = true;
                return result;
            }

            if (dryRun || result.Problems.Count > 0)
            {
                result.Action = existing == null ? "create" : "update";
                result.Success = result.Problems.Count == 0;
                return result;
            }

            var reply = _client.Query<QueryUpsertProxyReply>(new QueryUpsertProxy { Proxy = configuration, MatchByName = true });
            result.Success = reply.Success;
            result.Action = reply.Action;
            result.StartError = reply.StartError;
            if (reply.Message != null)
            {
                result.Problems.Add(reply.Message);
            }

            return result;
        }

        /// <summary>
        /// A copy of the proxy with its secrets replaced by <see cref="SecretMask"/>, for showing.
        /// </summary>
        public static NpProxyConfiguration MaskSecrets(NpProxyConfiguration configuration)
        {
            var copy = configuration.Clone();

            if (copy.TlsCertificatePassword.Length > 0)
            {
                copy.TlsCertificatePassword = SecretMask;
            }

            foreach (var host in copy.HostNames.Where(o => o.TlsCertificatePassword.Length > 0))
            {
                host.TlsCertificatePassword = SecretMask;
            }

            return copy;
        }

        private static void RestoreSecrets(NpProxyConfiguration configuration, NpProxyConfiguration? existing)
        {
            if (configuration.TlsCertificatePassword == SecretMask)
            {
                configuration.TlsCertificatePassword = existing?.TlsCertificatePassword ?? string.Empty;
            }

            foreach (var host in configuration.HostNames.Where(o => o.TlsCertificatePassword == SecretMask))
            {
                host.TlsCertificatePassword = existing?.HostNames
                    .FirstOrDefault(o => NpHostMatcher.Normalize(o.Host) == NpHostMatcher.Normalize(host.Host))?.TlsCertificatePassword ?? string.Empty;
            }
        }

        private static bool AreSame(NpProxyConfiguration a, NpProxyConfiguration b)
        {
            var left = a.Clone();
            var right = b.Clone();
            left.Id = right.Id = Guid.Empty;

            //The service keeps host names in one form, so they are compared in that form.
            foreach (var host in left.HostNames.Concat(right.HostNames))
            {
                host.Host = NpHostMatcher.Normalize(host.Host) ?? string.Empty;
            }

            return JsonConvert.SerializeObject(left) == JsonConvert.SerializeObject(right);
        }

        /// <summary>
        /// Starts, stops, restarts or deletes the proxy with this name. Returns why it could not be done, or null.
        /// </summary>
        public string? ProxyAction(string name, NpProxyAction action)
        {
            var reply = _client.Query<QueryActionReply>(new QueryProxyAction { Name = name, Action = action });
            return reply.Success ? null : reply.Message ?? "The service did not do it.";
        }

        public List<NpConnectionGridItem> GetConnections(Guid proxyId)
            => _client.Query<QueryProxyConnectionsReply>(new QueryProxyConnections(proxyId)).Collection;

        public int CloseConnections(Guid? connectionId, string? proxyName)
            => _client.Query<QueryCloseConnectionsReply>(new QueryCloseConnections { ConnectionId = connectionId, ProxyName = proxyName }).Closed;

        //---------------------------------------------------------------- Users

        public List<NpUser> ListUsers() => _client.Query<QueryUserListReply>(new QueryUserList()).Collection;

        public string? AddUser(string userName, string password, string description = "")
            => UserAction(NpUserAction.Add, userName, password, description);

        public string? RemoveUser(string userName) => UserAction(NpUserAction.Remove, userName, string.Empty, string.Empty);

        public string? SetPassword(string userName, string password) => UserAction(NpUserAction.SetPassword, userName, password, string.Empty);

        private string? UserAction(NpUserAction action, string userName, string password, string description)
        {
            var reply = _client.Query<QueryActionReply>(new QueryUserAction
            {
                Action = action,
                UserName = userName,
                PasswordHash = password.Length == 0 && action == NpUserAction.Remove ? string.Empty : NpUtility.Sha256(password),
                Description = description
            });

            return reply.Success ? null : reply.Message ?? "The service did not do it.";
        }

        //---------------------------------------------------------------- Plugins

        public List<NpPluginGridItem> ListPlugins() => _client.Query<QueryPluginListReply>(new QueryPluginList()).Collection;

        /// <summary>
        /// Adds the plugin, or replaces the one with the same name.
        /// </summary>
        public QueryUpsertPluginReply ApplyPlugin(NpPluginConfiguration plugin)
            => _client.Query<QueryUpsertPluginReply>(new QueryUpsertPlugin { Plugin = plugin, MatchByName = true });

        public string? PluginAction(string name, NpPluginAction action)
        {
            var reply = _client.Query<QueryActionReply>(new QueryPluginAction { Name = name, Action = action });
            return reply.Success ? null : reply.Message ?? "The service did not do it.";
        }

        //---------------------------------------------------------------- Export and import

        /// <summary>
        /// The proxies, plugins and settings. Users are not included: their passwords are hashes that cannot be set from outside.
        /// </summary>
        public NpExport Export()
            => new()
            {
                Proxies = GetAllProxies(),
                Plugins = ListPlugins().Select(o => new NpPluginConfiguration
                {
                    Id = o.Id,
                    Name = o.Name,
                    Description = o.Description,
                    Enabled = o.Enabled,
                    AssemblyPath = o.AssemblyPath,
                    TypeName = o.TypeName,
                    Settings = o.Settings,
                    TimeoutMilliseconds = o.TimeoutMilliseconds
                }).ToList(),
                Settings = GetSettings()
            };
    }
}
