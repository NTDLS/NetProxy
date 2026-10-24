using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using NetProxy.Service.Proxy;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace NetProxy.Service
{
    /// <summary>
    /// What the service offers for managing it without the console: the same operations, with an answer for each, and a way to
    /// apply changes that were made to the configuration files by hand.
    /// </summary>
    public partial class NpServiceManager
    {
        private readonly object _managementLock = new();
        private PosixSignalRegistration? _hangUpRegistration;

        public DateTime StartedUtc { get; } = DateTime.UtcNow;

        public sealed class ReloadResult
        {
            public bool Success { get; set; }
            public List<string> Changes { get; } = [];
            public List<string> Problems { get; } = [];
        }

        /// <summary>
        /// On Linux and macOS a hang up signal (kill -HUP) reloads the configuration files, the way services there usually do.
        /// </summary>
        private void RegisterReloadSignal()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                _hangUpRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
                {
                    context.Cancel = true;
                    Task.Run(() =>
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Information, "Reloading the configuration (SIGHUP).");
                        var result = ReloadConfiguration(dryRun: false);
                        foreach (var problem in result.Problems)
                        {
                            Singletons.Logging.Write(NpLogging.Severity.Warning, $"Reload: {problem}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"The reload signal (SIGHUP) could not be set up: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a proxy by id, otherwise by name (not case sensitive).
        /// </summary>
        public NpProxy? FindProxy(Guid? id, string? name)
        {
            var proxies = GetProxies();

            if (id != null && id != Guid.Empty)
            {
                var byId = proxies.FirstOrDefault(o => o.Configuration.Id == id);
                if (byId != null)
                {
                    return byId;
                }
            }

            return string.IsNullOrWhiteSpace(name)
                ? null : proxies.FirstOrDefault(o => o.Configuration.Name.Equals(name.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// The proxy that a configuration replaces. With <paramref name="matchByName"/> that is the one with the same name, and the
        /// configuration takes its id (or a new one if the id belongs to another proxy).
        /// </summary>
        private NpProxy? ResolveExisting(NpProxyConfiguration configuration, bool matchByName)
        {
            var existing = matchByName ? FindProxy(null, configuration.Name) : GetProxyById(configuration.Id);

            if (existing != null)
            {
                configuration.Id = existing.Configuration.Id;
                configuration.AdoptEndpointIds(existing.Configuration);
            }
            else if (matchByName && GetProxyById(configuration.Id) != null)
            {
                configuration.Id = Guid.NewGuid();
            }

            return existing;
        }

        public List<string> ValidateProxy(NpProxyConfiguration configuration, bool matchByName)
        {
            lock (_managementLock)
            {
                ResolveExisting(configuration, matchByName);
                return NpProxyValidator.Validate(configuration, GetProxies().Select(o => o.Configuration));
            }
        }

        /// <summary>
        /// Saves a proxy (replacing the one it is the new version of) and starts it if it is set to start automatically.
        /// Returns why it could not be saved, or null. <paramref name="startError"/> says why it was saved but did not start.
        /// </summary>
        public string? UpsertProxy(NpProxyConfiguration configuration, bool matchByName, out string action, out string? startError)
        {
            lock (_managementLock)
            {
                startError = null;
                action = string.Empty;

                var existing = ResolveExisting(configuration, matchByName);

                var problems = NpProxyValidator.Validate(configuration, GetProxies().Select(o => o.Configuration));
                if (problems.Count > 0)
                {
                    return string.Join(" ", problems);
                }

                if (existing != null)
                {
                    existing.Stop();
                    RemoveProxy(existing);
                }

                var proxy = new NpProxy(configuration);

                AddProxy(proxy);
                SaveConfiguration();

                action = existing != null ? "updated" : "created";
                Singletons.Logging.Write(NpLogging.Severity.Information, $"Proxy '{configuration.Name}' was {action}.");

                proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, existing != null ? NpChangeKind.Changed : NpChangeKind.Added)
                    .AsTask().GetAwaiter().GetResult();

                if (configuration.AutoStart && proxy.Start(out var error) == false)
                {
                    startError = error;
                }

                return null;
            }
        }

        /// <summary>
        /// Starts, stops, restarts or deletes a proxy. Returns why it could not be done, or null.
        /// </summary>
        public string? ApplyProxyAction(Guid? id, string? name, NpProxyAction action)
        {
            lock (_managementLock)
            {
                var proxy = FindProxy(id, name);
                if (proxy == null)
                {
                    return $"There is no proxy '{name ?? id?.ToString()}'.";
                }

                switch (action)
                {
                    case NpProxyAction.Start:
                        return proxy.Start(out var startError) ? null : startError;

                    case NpProxyAction.Stop:
                        proxy.Stop();
                        return null;

                    case NpProxyAction.Restart:
                        proxy.Stop();
                        return proxy.Start(out var restartError) ? null : restartError;

                    default:
                        proxy.Stop();
                        RemoveProxy(proxy);
                        SaveConfiguration();
                        Singletons.Logging.Write(NpLogging.Severity.Information, $"Proxy '{proxy.Configuration.Name}' was deleted.");
                        proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, NpChangeKind.Deleted).AsTask().GetAwaiter().GetResult();
                        return null;
                }
            }
        }

        /// <summary>
        /// Adds a user, removes one, or sets a password. Returns why it could not be done, or null.
        /// </summary>
        public string? ApplyUserAction(NpUserAction action, string userName, string passwordHash, string description)
        {
            userName = userName.Trim();

            if (userName.Length == 0)
            {
                return "The user needs a name.";
            }

            lock (Configuration)
            {
                var users = Configuration.Users.Collection;
                var existing = users.FirstOrDefault(o => o.UserName.Equals(userName, StringComparison.CurrentCultureIgnoreCase));

                switch (action)
                {
                    case NpUserAction.Add:
                        if (existing != null)
                        {
                            return $"There is already a user '{userName}'.";
                        }
                        if (string.IsNullOrEmpty(passwordHash))
                        {
                            return "A new user needs a password.";
                        }
                        users.Add(new NpUser { Id = Guid.NewGuid().ToString(), UserName = userName, PasswordHash = passwordHash, Description = description });
                        break;

                    case NpUserAction.SetPassword:
                        if (existing == null)
                        {
                            return $"There is no user '{userName}'.";
                        }
                        if (string.IsNullOrEmpty(passwordHash))
                        {
                            return "The password is required.";
                        }
                        existing.PasswordHash = passwordHash;
                        break;

                    default:
                        if (existing == null)
                        {
                            return $"There is no user '{userName}'.";
                        }
                        if (users.Count <= 1)
                        {
                            return "The last user cannot be removed: that would lock everyone out.";
                        }
                        users.Remove(existing);
                        break;
                }
            }

            SaveConfiguration();
            Singletons.Logging.Write(NpLogging.Severity.Information, $"User '{userName}': {action}.");
            return null;
        }

        /// <summary>
        /// Closes one connection, or all the connections of a proxy. Returns how many were closed.
        /// </summary>
        public int CloseConnections(Guid? connectionId, Guid? proxyId, string? proxyName)
        {
            var closed = 0;

            if (connectionId != null)
            {
                foreach (var proxy in GetProxies())
                {
                    if (proxy.ActiveConnections.TryGetValue(connectionId.Value, out var connection))
                    {
                        connection.Abort();
                        closed++;
                    }
                }

                return closed;
            }

            var target = FindProxy(proxyId, proxyName);
            if (target != null)
            {
                foreach (var connection in target.ActiveConnections.Values.ToList())
                {
                    connection.Abort();
                    closed++;
                }
            }

            return closed;
        }

        /// <summary>
        /// The certificates of the proxies, and when they expire.
        /// </summary>
        public List<NpCertificateStatus> GetCertificateStatus()
        {
            var result = new List<NpCertificateStatus>();

            foreach (var proxy in GetProxies())
            {
                var configuration = proxy.Configuration;

                if (string.IsNullOrWhiteSpace(configuration.TlsCertificatePath) == false)
                {
                    result.Add(ReadCertificate(configuration.Name, string.Empty, configuration.TlsCertificatePath, configuration.TlsCertificatePassword));
                }

                foreach (var host in configuration.HostNames.Where(o => string.IsNullOrWhiteSpace(o.TlsCertificatePath) == false))
                {
                    result.Add(ReadCertificate(configuration.Name, host.Host, host.TlsCertificatePath, host.TlsCertificatePassword));
                }
            }

            return result;
        }

        private static NpCertificateStatus ReadCertificate(string proxyName, string hostName, string path, string password)
        {
            var status = new NpCertificateStatus { ProxyName = proxyName, HostName = hostName, Path = path };

            try
            {
                using var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, string.IsNullOrEmpty(password) ? null : password);
                status.Subject = certificate.Subject;
                status.NotAfterUtc = certificate.NotAfter.ToUniversalTime();
                status.DaysLeft = (int)Math.Floor((certificate.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays);
            }
            catch (Exception ex)
            {
                status.Error = ex.GetBaseException().Message;
            }

            return status;
        }

        /// <summary>
        /// The last lines of the log file.
        /// </summary>
        public List<string> TailLog(int lines, out string? message)
        {
            message = null;
            lines = Math.Clamp(lines, 1, 5000);

            string path;
            lock (Configuration)
            {
                path = Configuration.Settings.LogPath;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                message = "The service does not log to a file.";
                return [];
            }

            path = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                //The end of a big file is enough.
                const int window = 4 * 1024 * 1024;
                if (stream.Length > window)
                {
                    stream.Seek(-window, SeekOrigin.End);
                }

                using var reader = new StreamReader(stream);
                var all = reader.ReadToEnd().Split('\n').Select(o => o.TrimEnd('\r')).Where(o => o.Length > 0).ToList();
                return all.Skip(Math.Max(0, all.Count - lines)).ToList();
            }
            catch (Exception ex)
            {
                message = $"The log file '{path}' could not be read: {ex.GetBaseException().Message}";
                return [];
            }
        }

        //---------------------------------------------------------------- Reload

        /// <summary>
        /// Applies what was changed in the configuration files. Proxies that did not change keep running and keep their connections.
        /// Nothing is changed if the files have a problem. With <paramref name="dryRun"/> nothing is changed at all.
        /// </summary>
        public ReloadResult ReloadConfiguration(bool dryRun)
        {
            lock (_managementLock)
            {
                var result = new ReloadResult();

                NpConfiguration fileConfiguration;
                List<NpProxyConfiguration> fileProxies;
                bool plainTextSecrets;

                try
                {
                    ReadConfigurationFromDisk(out fileConfiguration, out fileProxies, out plainTextSecrets);
                }
                catch (Exception ex)
                {
                    result.Problems.Add($"The configuration files could not be read: {ex.GetBaseException().Message}");
                    return result;
                }

                var current = GetProxies();

                //A file with no proxies is how a file that could not be understood looks. Deleting every proxy is done on purpose.
                if (fileProxies.Count == 0 && current.Count > 0)
                {
                    result.Problems.Add("The proxy file has no proxies, which is probably a mistake (delete the proxies with the proxy delete command if you mean it).");
                }

                if (fileConfiguration.Users.Collection.Count == 0)
                {
                    result.Problems.Add("The configuration file has no users, which would lock everyone out.");
                }

                if (result.Problems.Count > 0)
                {
                    return result;
                }

                //Which proxy of the file is which of the service: by id, and the ones that are left by name (a proxy whose id was dropped from the file).
                var matches = new Dictionary<NpProxyConfiguration, NpProxy>();
                var taken = new HashSet<NpProxy>();

                foreach (var desired in fileProxies)
                {
                    var existing = current.FirstOrDefault(o => o.Configuration.Id == desired.Id && taken.Contains(o) == false);
                    if (existing != null)
                    {
                        matches[desired] = existing;
                        taken.Add(existing);
                    }
                }

                foreach (var desired in fileProxies.Where(o => matches.ContainsKey(o) == false))
                {
                    var existing = current.FirstOrDefault(o => taken.Contains(o) == false
                        && o.Configuration.Name.Equals(desired.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (existing != null)
                    {
                        desired.Id = existing.Configuration.Id;
                        matches[desired] = existing;
                        taken.Add(existing);
                    }
                }

                foreach (var (desired, existing) in matches)
                {
                    desired.AdoptEndpointIds(existing.Configuration);
                }

                foreach (var desired in fileProxies)
                {
                    foreach (var problem in NpProxyValidator.Validate(desired, fileProxies))
                    {
                        result.Problems.Add(problem);
                    }
                }

                var pluginResult = Singletons.Plugins.ReloadFromDiskAsync(dryRun: true).GetAwaiter().GetResult();
                result.Problems.AddRange(pluginResult.Problems);

                if (result.Problems.Count > 0)
                {
                    return result;
                }

                var removed = current.Where(o => taken.Contains(o) == false).ToList();
                var added = fileProxies.Where(o => matches.ContainsKey(o) == false).ToList();
                var changed = fileProxies.Where(o => matches.TryGetValue(o, out var existing)
                    && JsonConvert.SerializeObject(existing.Configuration) != JsonConvert.SerializeObject(o)).ToList();

                foreach (var proxy in removed)
                {
                    result.Changes.Add($"Proxy '{proxy.Configuration.Name}': removed.");
                }
                foreach (var proxy in changed)
                {
                    result.Changes.Add($"Proxy '{proxy.Name}': changed.");
                }
                foreach (var proxy in added)
                {
                    result.Changes.Add($"Proxy '{proxy.Name}': added.");
                }

                bool usersChanged, settingsChanged;
                lock (Configuration)
                {
                    usersChanged = JsonConvert.SerializeObject(fileConfiguration.Users) != JsonConvert.SerializeObject(Configuration.Users);
                    settingsChanged = JsonConvert.SerializeObject(fileConfiguration.Settings) != JsonConvert.SerializeObject(Configuration.Settings);

                    if (fileConfiguration.ManagementPort != Configuration.ManagementPort)
                    {
                        result.Changes.Add($"Management port: {fileConfiguration.ManagementPort} (applies when the service restarts).");
                    }
                }

                if (usersChanged)
                {
                    result.Changes.Add("Users: changed.");
                }
                if (settingsChanged)
                {
                    result.Changes.Add("Settings: changed.");
                }
                result.Changes.AddRange(pluginResult.Changes);

                result.Success = true;

                if (dryRun)
                {
                    return result;
                }

                //Stopped first, so that the ports they had are free for the proxies that take them.
                var restart = new List<(NpProxyConfiguration Configuration, bool WasRunning)>();

                foreach (var proxy in removed)
                {
                    proxy.Stop();
                    RemoveProxy(proxy);
                    proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, NpChangeKind.Deleted).AsTask().GetAwaiter().GetResult();
                }

                foreach (var desired in changed)
                {
                    var existing = matches[desired];
                    var wasRunning = existing.IsRunning;
                    existing.Stop();
                    RemoveProxy(existing);
                    restart.Add((desired, wasRunning));
                }

                foreach (var desired in added)
                {
                    restart.Add((desired, false));
                }

                foreach (var (configuration, wasRunning) in restart)
                {
                    var proxy = new NpProxy(configuration);
                    AddProxy(proxy);

                    proxy.Plugins.ProxyConfigurationChangedAsync(proxy.Info, matches.Values.Any(o => o.Configuration.Id == configuration.Id) ? NpChangeKind.Changed : NpChangeKind.Added)
                        .AsTask().GetAwaiter().GetResult();

                    if ((wasRunning || configuration.AutoStart) && proxy.Start(out var error) == false)
                    {
                        result.Problems.Add($"Proxy '{configuration.Name}' could not be started: {error}");
                    }
                }

                if (usersChanged)
                {
                    lock (Configuration)
                    {
                        Configuration.Users = fileConfiguration.Users;
                    }
                }

                if (fileConfiguration.ManagementPort != Configuration.ManagementPort)
                {
                    lock (Configuration)
                    {
                        Configuration.ManagementPort = fileConfiguration.ManagementPort;
                    }
                }

                if (settingsChanged)
                {
                    ApplySettings(fileConfiguration.Settings);
                }

                Singletons.Plugins.ReloadFromDiskAsync(dryRun: false).GetAwaiter().GetResult();

                //Saves the plain text secrets that were typed into the files encrypted, and fills in what the files left out (ids).
                SaveConfiguration();

                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"The configuration was reloaded: {(result.Changes.Count == 0 ? "no changes" : string.Join(" ", result.Changes))}");

                return result;
            }
        }
    }
}
