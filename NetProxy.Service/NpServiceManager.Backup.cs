using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.PluginApi;
using NetProxy.Service.Proxy;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Service
{
    /// <summary>
    /// Export and import of the whole configuration.
    /// </summary>
    public partial class NpServiceManager
    {
        private static readonly JsonSerializerSettings _backupFormat = new()
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Everything the service is set up with, as the JSON document of <see cref="NpConfigurationBackup"/>.
        /// </summary>
        public string ExportConfiguration()
        {
            var backup = new NpConfigurationBackup { ServiceVersion = typeof(NpServiceManager).Assembly.GetName().Version?.ToString(3) ?? string.Empty };

            lock (Configuration)
            {
                backup.ManagementPort = Configuration.ManagementPort;
                backup.Settings = JsonConvert.DeserializeObject<NpServiceSettings>(JsonConvert.SerializeObject(Configuration.Settings)) ?? new();
                backup.Users = Configuration.Users.Collection.Select(o => new NpUser
                {
                    Id = o.Id,
                    UserName = o.UserName,
                    PasswordHash = o.PasswordHash,
                    Description = o.Description
                }).ToList();
            }

            backup.Certificates = Singletons.Certificates.GetGridItems().Select(o => new NpCertificateConfiguration
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                Path = o.Path,
                Password = o.Password
            }).ToList();

            backup.Plugins = Singletons.Plugins.GetGridItems().Select(o => new NpPluginConfiguration
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                Enabled = o.Enabled,
                AssemblyPath = o.AssemblyPath,
                TypeName = o.TypeName,
                Settings = new Dictionary<string, string>(o.Settings),
                TimeoutMilliseconds = o.TimeoutMilliseconds
            }).ToList();

            backup.Proxies = GetProxies().Select(o => o.Configuration.Clone()).OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            return JsonConvert.SerializeObject(backup, _backupFormat);
        }

        /// <summary>
        /// Reads and checks a document. Returns the backup, or null with what is wrong with it.
        /// </summary>
        private static NpConfigurationBackup? ReadBackup(string document, List<string> problems)
        {
            NpConfigurationBackup? backup;

            try
            {
                backup = string.IsNullOrWhiteSpace(document) ? null : JsonConvert.DeserializeObject<NpConfigurationBackup>(document);
            }
            catch (Exception ex)
            {
                problems.Add($"The file is not a NetProxy configuration: {ex.GetBaseException().Message}");
                return null;
            }

            if (backup == null)
            {
                problems.Add("The file is empty.");
                return null;
            }

            if (backup.FormatVersion != NpConfigurationBackup.CurrentFormatVersion)
            {
                problems.Add($"The file is of format {backup.FormatVersion}, which this version does not read.");
                return null;
            }

            if (backup.Users.Count == 0 || backup.Users.Any(o => string.IsNullOrWhiteSpace(o.UserName)))
            {
                problems.Add("The configuration needs at least one user, and every user needs a name: without one nobody could log in.");
            }

            if (backup.Users.GroupBy(o => o.UserName.Trim(), StringComparer.CurrentCultureIgnoreCase).Any(o => o.Count() > 1))
            {
                problems.Add("A user is listed more than once.");
            }

            if (backup.ManagementPort is < 1 or > 65535)
            {
                problems.Add("The management port must be between 1 and 65535.");
            }

            foreach (var certificate in backup.Certificates)
            {
                if (string.IsNullOrWhiteSpace(certificate.Name) || string.IsNullOrWhiteSpace(certificate.Path))
                {
                    problems.Add($"Certificate '{certificate.Name}': it needs a name and a PFX file.");
                }
            }

            foreach (var duplicate in backup.Certificates.GroupBy(o => o.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Where(o => o.Count() > 1))
            {
                problems.Add($"There is more than one certificate named '{duplicate.Key}'.");
            }

            foreach (var plugin in backup.Plugins)
            {
                if (string.IsNullOrWhiteSpace(plugin.Name) || string.IsNullOrWhiteSpace(plugin.AssemblyPath))
                {
                    problems.Add($"Plugin '{plugin.Name}': it needs a name and an assembly.");
                }
            }

            foreach (var duplicate in backup.Plugins.GroupBy(o => o.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Where(o => o.Count() > 1))
            {
                problems.Add($"There is more than one plugin named '{duplicate.Key}'.");
            }

            var certificateNames = backup.Certificates.Select(o => o.Name.Trim()).ToList();
            var pluginIds = backup.Plugins.Select(o => o.Id).ToHashSet();

            foreach (var proxy in backup.Proxies)
            {
                //A plugin that is not in the document cannot be used by a proxy.
                proxy.PluginIds.RemoveAll(o => pluginIds.Contains(o) == false);

                problems.AddRange(NpProxyValidator.Validate(proxy, backup.Proxies, certificateNames));
            }

            return backup;
        }

        /// <summary>
        /// Puts the configuration of a document in place of the current one. The document is checked first and nothing is changed if it is
        /// not good. Then the proxies are stopped, the current configuration is replaced, and the proxies that start automatically are started.
        /// </summary>
        public QueryImportConfigurationReply ImportConfiguration(string document, bool dryRun)
        {
            var reply = new QueryImportConfigurationReply();

            var backup = ReadBackup(document, reply.Problems);
            if (backup == null || reply.Problems.Count > 0)
            {
                return reply;
            }

            reply.Proxies = backup.Proxies.Count;
            reply.Certificates = backup.Certificates.Count;
            reply.Plugins = backup.Plugins.Count;
            reply.Users = backup.Users.Count;

            if (dryRun)
            {
                reply.Success = true;
                return reply;
            }

            lock (_managementLock)
            {
                //What was there is kept, in case the import was a mistake.
                reply.PreviousConfigurationFile = KeepCurrentConfiguration();

                foreach (var proxy in GetProxies())
                {
                    proxy.Stop();
                    RemoveProxy(proxy);
                    proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, NpChangeKind.Deleted).AsTask().GetAwaiter().GetResult();
                }

                Singletons.Certificates.ReplaceAll(backup.Certificates);
                Singletons.Plugins.ReplaceAllAsync(backup.Plugins).GetAwaiter().GetResult();

                var settings = backup.Settings;
                settings.LogFileMaxSizeMB = Math.Clamp(settings.LogFileMaxSizeMB, 1, 10240);
                settings.LogFileRetainedCount = Math.Clamp(settings.LogFileRetainedCount, 0, 1000);
                settings.LogPath = (settings.LogPath ?? string.Empty).Trim();

                lock (Configuration)
                {
                    Configuration.Users = new NpUsers { Collection = backup.Users };
                    Configuration.ManagementPort = backup.ManagementPort;
                }

                ApplySettings(settings);

                var proxies = new List<NpProxy>();
                foreach (var configuration in backup.Proxies)
                {
                    var proxy = new NpProxy(configuration);
                    AddProxy(proxy);
                    proxies.Add(proxy);
                    proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, NpChangeKind.Added).AsTask().GetAwaiter().GetResult();
                }

                SaveConfiguration();

                foreach (var proxy in proxies.Where(o => o.Configuration.AutoStart))
                {
                    if (proxy.Start(out var error) == false)
                    {
                        reply.Problems.Add($"Proxy '{proxy.Configuration.Name}' could not be started: {error}");
                    }
                }

                reply.Success = true;
                return reply;
            }
        }

        /// <summary>
        /// Writes what the service is set up with to the Backups folder of the configuration, and returns the file.
        /// </summary>
        private string? KeepCurrentConfiguration()
        {
            try
            {
                var folder = Path.Combine(
                    OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) : "/usr/share",
                    Singletons.ConfigurationName, "Backups");
                Directory.CreateDirectory(folder);

                var file = Path.Combine(folder, $"before-import-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
                File.WriteAllText(file, ExportConfiguration());
                return file;
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("The configuration could not be kept before the import", ex);
                return null;
            }
        }
    }
}
