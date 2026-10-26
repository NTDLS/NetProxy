using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NTDLS.Persistence;
using System.Security.Cryptography.X509Certificates;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// The TLS certificates of the service. They are set up once, with a name, and the proxies and their host names refer to them
    /// by that name. Each PFX file is read once and watched: when it is replaced (a renewal with win-acme or certbot, for example)
    /// the new certificate is used for new connections of every proxy that refers to it, without anything being restarted. The
    /// connections that are open keep the certificate they started with.
    /// </summary>
    internal sealed class NpCertificateManager : IDisposable
    {
        /// <summary>
        /// A file that changes is not read until it has been quiet for this long: renewal tools write it in pieces.
        /// </summary>
        private static readonly TimeSpan _settleTime = TimeSpan.FromSeconds(3);

        /// <summary>
        /// A certificate that could not be read (it may still be being written) is tried again after this long.
        /// </summary>
        private static readonly TimeSpan _retryTime = TimeSpan.FromSeconds(10);

        private const int MaxRetries = 6;

        /// <summary>
        /// The files are looked at now and then as well, because a watcher can miss changes (network shares, for one).
        /// </summary>
        private static readonly TimeSpan _pollTime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Certificates that were replaced are let go of after this long, when no handshake can still be using them.
        /// </summary>
        private static readonly TimeSpan _disposeDelay = TimeSpan.FromMinutes(10);

        /// <summary>
        /// One certificate: its configuration, what was read from the file, and the watching of the file.
        /// </summary>
        private sealed class Entry : IDisposable
        {
            private readonly object _reloadLock = new();
            private FileSystemWatcher? _watcher;
            private Timer? _settleTimer;
            private (DateTime Written, long Length) _stamp;
            private X509Certificate2? _certificate;
            private int _retries;
            private bool _disposed;

            public NpCertificateConfiguration Configuration { get; }
            public string FullPath { get; }
            public string Error { get; private set; } = string.Empty;

            public X509Certificate2? Certificate => Volatile.Read(ref _certificate);

            public Entry(NpCertificateConfiguration configuration)
            {
                Configuration = configuration;
                FullPath = ResolvePath(configuration.Path);
            }

            /// <summary>
            /// Reads the file. A file that cannot be read leaves the certificate that was read before in use.
            /// </summary>
            public bool Load()
            {
                lock (_reloadLock)
                {
                    //The stamp is taken before the file is read, so that a change during the read is seen as a change.
                    var stamp = Stamp(FullPath);

                    try
                    {
                        var certificate = X509CertificateLoader.LoadPkcs12FromFile(FullPath,
                            string.IsNullOrEmpty(Configuration.Password) ? null : Configuration.Password);

                        _stamp = stamp;
                        Error = string.Empty;
                        Replace(certificate);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Error = ex.GetBaseException().Message;
                        return false;
                    }
                }
            }

            private void Replace(X509Certificate2 certificate)
            {
                var old = Interlocked.Exchange(ref _certificate, certificate);

                if (old != null)
                {
                    //Connections that are open keep the old one in use until they end, so it is only let go of later.
                    Task.Delay(_disposeDelay).ContinueWith(_ => old.Dispose());
                }
            }

            public void StartWatching()
            {
                try
                {
                    _watcher = new FileSystemWatcher(Path.GetDirectoryName(FullPath)!, Path.GetFileName(FullPath))
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    _watcher.Changed += (_, _) => Changed();
                    _watcher.Created += (_, _) => Changed();
                    _watcher.Renamed += (_, _) => Changed();
                    _watcher.Deleted += (_, _) => Changed();
                    _watcher.Error += (_, _) => Changed();
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning,
                        $"Certificate '{Configuration.Name}': the file '{FullPath}' cannot be watched ({ex.Message}). It is checked every {_pollTime.TotalSeconds:0} seconds instead.");
                }

                _settleTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            }

            /// <summary>
            /// Called now and then, for the changes that a watcher does not see.
            /// </summary>
            public void Poll()
            {
                if (Stamp(FullPath) != _stamp)
                {
                    Changed();
                }
            }

            private void Changed()
            {
                lock (_reloadLock)
                {
                    _retries = 0;
                    _settleTimer?.Change(_settleTime, Timeout.InfiniteTimeSpan);
                }
            }

            private void Reload()
            {
                lock (_reloadLock)
                {
                    if (_disposed || Stamp(FullPath) == _stamp)
                    {
                        return; //Nothing changed after all.
                    }

                    var hadCertificate = Certificate != null;

                    if (Load())
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Information,
                            $"Certificate '{Configuration.Name}': the file was reloaded. New connections use it.");
                        return;
                    }

                    //The previous certificate keeps being used. The file may still be being written, so it is tried again for a while.
                    if (_retries++ < MaxRetries)
                    {
                        _settleTimer?.Change(_retryTime, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Warning,
                            $"Certificate '{Configuration.Name}': the changed file cannot be used"
                            + (hadCertificate ? ", so the previous certificate stays in use" : string.Empty) + $". {Error}");
                    }
                }
            }

            public void Dispose()
            {
                lock (_reloadLock)
                {
                    _disposed = true;
                }

                _watcher?.Dispose();
                _settleTimer?.Dispose();

                var certificate = Interlocked.Exchange(ref _certificate, null);
                if (certificate != null)
                {
                    Task.Delay(_disposeDelay).ContinueWith(_ => certificate.Dispose());
                }
            }
        }

        private readonly object _lock = new();
        private readonly object _saveLock = new();
        private Dictionary<string, Entry> _byName = new(StringComparer.OrdinalIgnoreCase);
        private List<Entry> _entries = [];
        private Timer? _pollTimer;

        /// <summary>
        /// The proxies that exist, to tell which of them use a certificate.
        /// </summary>
        public Func<IEnumerable<NpProxyConfiguration>> Proxies { get; set; } = () => [];

        /// <summary>
        /// Called with the old and the new name when a certificate is renamed, so that the proxies that use it follow.
        /// </summary>
        public Action<string, string> Renamed { get; set; } = (_, _) => { };

        private static string ResolvePath(string path)
            => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));

        private static (DateTime Written, long Length) Stamp(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists ? (info.LastWriteTimeUtc, info.Length) : (DateTime.MinValue, -1);
            }
            catch
            {
                return (DateTime.MinValue, -1);
            }
        }

        //---------------------------------------------------------------- Use

        /// <summary>
        /// The current certificate of that name, or null if there is none or its file cannot be used.
        /// </summary>
        public X509Certificate2? Get(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Entry? entry;
            lock (_lock)
            {
                _byName.TryGetValue(name.Trim(), out entry);
            }

            return entry?.Certificate;
        }

        public bool Exists(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            lock (_lock)
            {
                return _byName.ContainsKey(name.Trim());
            }
        }

        /// <summary>
        /// Why a proxy cannot use the certificate of that name (it is not set up, or its file cannot be read), or null if it can.
        /// </summary>
        public string? GetProblem(string name)
        {
            Entry? entry;
            lock (_lock)
            {
                _byName.TryGetValue(name.Trim(), out entry);
            }

            if (entry == null)
            {
                return $"The TLS certificate '{name}' is not set up for the service.";
            }

            return entry.Certificate == null
                ? $"The TLS certificate '{name}' ('{entry.FullPath}') could not be loaded: {entry.Error}"
                : null;
        }

        //---------------------------------------------------------------- Start and stop

        /// <summary>
        /// Reads the certificate configuration and the certificate files, and starts watching the files. A file that cannot be read does
        /// not stop the service: the proxies that use it say so when they start.
        /// </summary>
        public void Start()
        {
            ReadFromDisk(out var configurations, out var hasPlainTextSecrets);

            var entries = new List<Entry>();
            foreach (var configuration in configurations)
            {
                entries.Add(CreateEntry(configuration));
            }

            Entry[] previous;
            lock (_lock)
            {
                previous = [.. _entries];
                _entries = entries;
                _byName = Index(entries);
            }

            //A service that is started again in the same process (a test does) lets go of what it had.
            foreach (var entry in previous)
            {
                entry.Dispose();
            }

            if (hasPlainTextSecrets)
            {
                Save();
            }

            if (entries.Count > 0)
            {
                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"Loaded {entries.Count(o => o.Certificate != null)} of {entries.Count} certificates.");
            }

            _pollTimer?.Dispose();
            _pollTimer = new Timer(_ => Poll(), null, _pollTime, _pollTime);
        }

        private static Dictionary<string, Entry> Index(IEnumerable<Entry> entries)
        {
            var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                result[entry.Configuration.Name] = entry;
            }
            return result;
        }

        private static Entry CreateEntry(NpCertificateConfiguration configuration)
        {
            var entry = new Entry(configuration);

            if (entry.Load() == false)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning,
                    $"Certificate '{configuration.Name}': the file '{entry.FullPath}' could not be loaded: {entry.Error}");
            }

            entry.StartWatching();
            return entry;
        }

        private void Poll()
        {
            Entry[] entries;
            lock (_lock)
            {
                entries = [.. _entries];
            }

            foreach (var entry in entries)
            {
                entry.Poll();
            }
        }

        public void Dispose()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;

            lock (_lock)
            {
                foreach (var entry in _entries)
                {
                    entry.Dispose();
                }

                _entries = [];
                _byName = new(StringComparer.OrdinalIgnoreCase);
            }
        }

        //---------------------------------------------------------------- Storage

        private static void ReadFromDisk(out List<NpCertificateConfiguration> configurations, out bool hasPlainTextSecrets)
        {
            configurations = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, new List<NpCertificateConfiguration>());
            hasPlainTextSecrets = false;

            foreach (var configuration in configurations)
            {
                configuration.Password = SecretProtector.Unprotect(configuration.Password,
                    $"the password of certificate '{configuration.Name}'", out var plain) ?? string.Empty;
                hasPlainTextSecrets |= plain;
            }
        }

        /// <summary>
        /// The names of the certificates in the file, which is what the proxies of a changed proxy file are checked against.
        /// </summary>
        public List<string> ReadNamesFromDisk()
        {
            try
            {
                ReadFromDisk(out var configurations, out _);
                return configurations.Select(o => o.Name).ToList();
            }
            catch
            {
                return [];
            }
        }

        private void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    List<NpCertificateConfiguration> configurations;
                    lock (_lock)
                    {
                        //Written with the passwords encrypted, from copies because the ones in use keep the real values.
                        configurations = _entries.Select(o => new NpCertificateConfiguration
                        {
                            Id = o.Configuration.Id,
                            Name = o.Configuration.Name,
                            Description = o.Configuration.Description,
                            Path = o.Configuration.Path,
                            Password = SecretProtector.Protect(o.Configuration.Password) ?? string.Empty
                        }).ToList();
                    }

                    CommonApplicationData.SaveToDisk(Singletons.ConfigurationName, configurations);
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write("Failed to save the certificates", ex);
                }
            }
        }

        //---------------------------------------------------------------- Management

        public List<NpCertificateGridItem> GetGridItems()
        {
            Entry[] entries;
            lock (_lock)
            {
                entries = [.. _entries];
            }

            var proxies = Proxies().ToList();
            var result = new List<NpCertificateGridItem>();

            foreach (var entry in entries)
            {
                var item = new NpCertificateGridItem
                {
                    Id = entry.Configuration.Id,
                    Name = entry.Configuration.Name,
                    Description = entry.Configuration.Description,
                    Path = entry.Configuration.Path,
                    Password = entry.Configuration.Password,
                    Error = entry.Error,
                    UsedBy = FindUsers(proxies, entry.Configuration.Name)
                };

                var certificate = entry.Certificate;
                if (certificate != null)
                {
                    item.IsLoaded = true;
                    item.Subject = certificate.Subject;
                    item.Thumbprint = certificate.Thumbprint;
                    item.NotAfterUtc = certificate.NotAfter.ToUniversalTime();
                    item.DaysLeft = (int)Math.Floor((certificate.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays);
                }

                result.Add(item);
            }

            return result;
        }

        private static bool Uses(NpProxyConfiguration proxy, string name)
            => string.Equals(proxy.TlsCertificate?.Trim(), name, StringComparison.OrdinalIgnoreCase)
               || proxy.HostNames.Any(o => string.Equals(o.TlsCertificate?.Trim(), name, StringComparison.OrdinalIgnoreCase));

        private static List<string> FindUsers(IEnumerable<NpProxyConfiguration> proxies, string name)
            => proxies.Where(o => Uses(o, name)).Select(o => o.Name).OrderBy(o => o, StringComparer.CurrentCultureIgnoreCase).ToList();

        /// <summary>
        /// Adds a certificate, or replaces the one with the same id. Returns what is wrong, or null when it was saved (the file may
        /// still be unusable: <paramref name="loadError"/> says why).
        /// </summary>
        public string? Upsert(NpCertificateConfiguration configuration, out string? loadError, out bool created)
        {
            loadError = null;
            created = true;

            configuration.Name = configuration.Name?.Trim() ?? string.Empty;
            configuration.Path = configuration.Path?.Trim() ?? string.Empty;
            configuration.Description = configuration.Description?.Trim() ?? string.Empty;
            configuration.Password ??= string.Empty;

            if (configuration.Name.Length == 0)
            {
                return "The certificate needs a name.";
            }

            if (configuration.Path.Length == 0)
            {
                return "The certificate needs a PFX file.";
            }

            string? oldName = null;
            Entry? existing;

            lock (_lock)
            {
                if (_entries.Any(o => o.Configuration.Id != configuration.Id
                    && o.Configuration.Name.Equals(configuration.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return $"There is already a certificate named '{configuration.Name}'.";
                }

                existing = _entries.FirstOrDefault(o => o.Configuration.Id == configuration.Id);
                if (existing != null)
                {
                    created = false;
                    oldName = existing.Configuration.Name;
                }
            }

            var entry = CreateEntry(configuration);
            loadError = entry.Certificate == null ? entry.Error : null;

            lock (_lock)
            {
                var index = existing != null ? _entries.IndexOf(existing) : -1;
                if (index >= 0)
                {
                    _entries[index] = entry;
                }
                else
                {
                    _entries.Add(entry);
                }

                _byName = Index(_entries);
            }

            existing?.Dispose();

            if (oldName != null && string.Equals(oldName, configuration.Name, StringComparison.Ordinal) == false)
            {
                Renamed(oldName, configuration.Name);
            }

            Save();
            return null;
        }

        /// <summary>
        /// Removes a certificate. Returns why it was not removed (proxies use it), or null.
        /// </summary>
        public string? Delete(Guid id, out bool found)
        {
            Entry? entry;
            lock (_lock)
            {
                entry = _entries.FirstOrDefault(o => o.Configuration.Id == id);
            }

            found = entry != null;
            if (entry == null)
            {
                return null;
            }

            var users = FindUsers(Proxies(), entry.Configuration.Name);
            if (users.Count > 0)
            {
                return $"The certificate '{entry.Configuration.Name}' is used by {string.Join(", ", users.Select(o => $"'{o}'"))}. Change those proxies first.";
            }

            lock (_lock)
            {
                _entries.Remove(entry);
                _byName = Index(_entries);
            }

            entry.Dispose();
            Save();
            return null;
        }

        /// <summary>
        /// Replaces all of the certificates by these. The files are read again, and nothing checks whether proxies use what goes: the
        /// proxies are replaced along with them.
        /// </summary>
        public void ReplaceAll(IEnumerable<NpCertificateConfiguration> configurations)
        {
            var entries = configurations.Select(CreateEntry).ToList();

            Entry[] previous;
            lock (_lock)
            {
                previous = [.. _entries];
                _entries = entries;
                _byName = Index(entries);
            }

            foreach (var entry in previous)
            {
                entry.Dispose();
            }

            Save();
        }

        public sealed class FileReloadResult
        {
            public List<string> Changes { get; } = [];
            public List<string> Problems { get; } = [];
        }

        /// <summary>
        /// Applies what was changed in the certificate file by hand: certificates that were added, changed (the file is read again) and
        /// removed. With <paramref name="dryRun"/> it only says what it would do.
        /// </summary>
        public FileReloadResult ReloadFromDisk(bool dryRun, IEnumerable<NpProxyConfiguration>? desiredProxies = null)
        {
            var result = new FileReloadResult();

            List<NpCertificateConfiguration> desired;
            try
            {
                ReadFromDisk(out desired, out _);
            }
            catch (Exception ex)
            {
                result.Problems.Add($"The certificate file could not be read: {ex.GetBaseException().Message}");
                return result;
            }

            Entry[] current;
            lock (_lock)
            {
                current = [.. _entries];
            }

            //A file with no certificates is how a file that could not be understood looks. Deleting every certificate is done on purpose.
            if (desired.Count == 0 && current.Length > 0)
            {
                result.Problems.Add("The certificate file has no certificates, which is probably a mistake (delete the certificates with the cert remove command if you mean it).");
                return result;
            }

            var matches = new Dictionary<NpCertificateConfiguration, Entry>();
            var taken = new HashSet<Entry>();

            foreach (var certificate in desired)
            {
                var existing = current.FirstOrDefault(o => o.Configuration.Id == certificate.Id && taken.Contains(o) == false);
                if (existing != null)
                {
                    matches[certificate] = existing;
                    taken.Add(existing);
                }
            }

            foreach (var certificate in desired.Where(o => matches.ContainsKey(o) == false))
            {
                var existing = current.FirstOrDefault(o => taken.Contains(o) == false
                    && o.Configuration.Name.Equals(certificate.Name, StringComparison.CurrentCultureIgnoreCase));
                if (existing != null)
                {
                    certificate.Id = existing.Configuration.Id;
                    matches[certificate] = existing;
                    taken.Add(existing);
                }
            }

            foreach (var certificate in desired)
            {
                if (string.IsNullOrWhiteSpace(certificate.Name) || string.IsNullOrWhiteSpace(certificate.Path))
                {
                    result.Problems.Add($"Certificate '{certificate.Name}': it needs a name and a PFX file.");
                }
                else if (desired.Count(o => o.Name.Equals(certificate.Name, StringComparison.CurrentCultureIgnoreCase)) > 1)
                {
                    result.Problems.Add($"There is more than one certificate named '{certificate.Name}'.");
                }
            }

            var removed = current.Where(o => taken.Contains(o) == false).ToList();
            var proxies = (desiredProxies ?? Proxies()).ToList();
            foreach (var entry in removed)
            {
                var users = FindUsers(proxies, entry.Configuration.Name);
                if (users.Count > 0)
                {
                    result.Problems.Add($"Certificate '{entry.Configuration.Name}' cannot be removed: {string.Join(", ", users.Select(o => $"'{o}'"))} use it.");
                }
            }

            if (result.Problems.Count > 0)
            {
                return result;
            }

            var added = desired.Where(o => matches.ContainsKey(o) == false).ToList();
            var changed = desired.Where(o => matches.TryGetValue(o, out var existing)
                && Newtonsoft.Json.JsonConvert.SerializeObject(existing.Configuration) != Newtonsoft.Json.JsonConvert.SerializeObject(o)).ToList();

            result.Changes.AddRange(removed.Select(o => $"Certificate '{o.Configuration.Name}': removed."));
            result.Changes.AddRange(changed.Select(o => $"Certificate '{o.Name}': changed."));
            result.Changes.AddRange(added.Select(o => $"Certificate '{o.Name}': added."));

            if (dryRun || result.Changes.Count == 0)
            {
                return result;
            }

            var renames = changed.Where(o => string.Equals(matches[o].Configuration.Name, o.Name, StringComparison.Ordinal) == false)
                .Select(o => (Old: matches[o].Configuration.Name, New: o.Name)).ToList();

            var next = new List<Entry>();
            foreach (var certificate in desired)
            {
                if (matches.TryGetValue(certificate, out var existing) && changed.Contains(certificate) == false)
                {
                    next.Add(existing);
                }
                else
                {
                    next.Add(CreateEntry(certificate));
                }
            }

            lock (_lock)
            {
                _entries = next;
                _byName = Index(next);
            }

            foreach (var entry in removed.Concat(changed.Select(o => matches[o])))
            {
                entry.Dispose();
            }

            foreach (var (oldName, newName) in renames)
            {
                Renamed(oldName, newName);
            }

            Save();
            return result;
        }
    }
}
