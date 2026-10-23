using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Security.Cryptography.X509Certificates;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// The TLS certificates of a proxy: the one of the proxy and the ones of its host names. The PFX files are watched, so that a
    /// certificate that is renewed (a new file in its place) is used for new connections without the proxy being restarted. The
    /// connections that are open keep the certificate they started with.
    /// </summary>
    internal sealed class NpCertificateStore : IDisposable
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

        private sealed record Source(string Path, string Password, string? Host);

        private sealed class Snapshot(X509Certificate2? defaultCertificate, Dictionary<string, X509Certificate2> hosts,
            Dictionary<string, (DateTime Written, long Length)> stamps)
        {
            public X509Certificate2? Default { get; } = defaultCertificate;
            public Dictionary<string, X509Certificate2> Hosts { get; } = hosts;
            public Dictionary<string, (DateTime Written, long Length)> Stamps { get; } = stamps;

            public IEnumerable<X509Certificate2> All => Hosts.Values.Concat(Default == null ? [] : [Default]);
        }

        private readonly string _proxyName;
        private readonly List<Source> _sources;
        private readonly object _reloadLock = new();
        private readonly List<FileSystemWatcher> _watchers = [];
        private Timer? _settleTimer;
        private Timer? _pollTimer;
        private Snapshot _snapshot;
        private int _retries;
        private bool _disposed;

        private NpCertificateStore(string proxyName, List<Source> sources, Snapshot snapshot)
        {
            _proxyName = proxyName;
            _sources = sources;
            _snapshot = snapshot;
        }

        /// <summary>
        /// The certificate of the proxy, if it has one.
        /// </summary>
        public X509Certificate2? Default => Volatile.Read(ref _snapshot).Default;

        /// <summary>
        /// The certificate to present to a client that asks for a host name: the one of the most specific host name that matches and
        /// has its own, otherwise the certificate of the proxy. Null if there is none.
        /// </summary>
        public X509Certificate2? Get(string? host)
        {
            var snapshot = Volatile.Read(ref _snapshot);
            var normalized = NpHostMatcher.Normalize(host);

            if (normalized != null && snapshot.Hosts.Count > 0)
            {
                var pattern = NpHostMatcher.FindBest(snapshot.Hosts.Keys, normalized);
                if (pattern != null)
                {
                    return snapshot.Hosts[pattern];
                }
            }

            return snapshot.Default;
        }

        /// <summary>
        /// Loads the certificates of the proxy, and returns null if it has none. Throws (with the reason) if one cannot be loaded.
        /// </summary>
        public static NpCertificateStore? Create(NpProxyConfiguration configuration)
        {
            var sources = new List<Source>();

            if (string.IsNullOrWhiteSpace(configuration.TlsCertificatePath) == false)
            {
                sources.Add(new Source(configuration.TlsCertificatePath, configuration.TlsCertificatePassword, null));
            }

            foreach (var host in configuration.HostNames.Where(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificatePath) == false))
            {
                var pattern = NpHostMatcher.Normalize(host.Host);
                if (pattern != null)
                {
                    sources.Add(new Source(host.TlsCertificatePath, host.TlsCertificatePassword, pattern));
                }
            }

            return sources.Count == 0 ? null : new NpCertificateStore(configuration.Name, sources, Load(sources));
        }

        private static Snapshot Load(List<Source> sources)
        {
            X509Certificate2? defaultCertificate = null;
            var hosts = new Dictionary<string, X509Certificate2>();
            var stamps = new Dictionary<string, (DateTime, long)>();

            try
            {
                foreach (var source in sources)
                {
                    //The stamp is taken before the file is read, so that a change during the read is seen as a change.
                    stamps[source.Path] = Stamp(source.Path);

                    X509Certificate2 certificate;
                    try
                    {
                        certificate = X509CertificateLoader.LoadPkcs12FromFile(source.Path, string.IsNullOrEmpty(source.Password) ? null : source.Password);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"The TLS certificate '{source.Path}' could not be loaded: {ex.GetBaseException().Message}", ex);
                    }

                    if (source.Host == null)
                    {
                        defaultCertificate = certificate;
                    }
                    else
                    {
                        hosts[source.Host] = certificate;
                    }
                }
            }
            catch
            {
                defaultCertificate?.Dispose();
                foreach (var certificate in hosts.Values)
                {
                    certificate.Dispose();
                }
                throw;
            }

            return new Snapshot(defaultCertificate, hosts, stamps);
        }

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

        //---------------------------------------------------------------- Watching

        /// <summary>
        /// Starts watching the certificate files.
        /// </summary>
        public void StartWatching()
        {
            foreach (var path in _sources.Select(o => o.Path).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    var watcher = new FileSystemWatcher(Path.GetDirectoryName(full)!, Path.GetFileName(full))
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += (_, _) => Changed();
                    watcher.Created += (_, _) => Changed();
                    watcher.Renamed += (_, _) => Changed();
                    watcher.Deleted += (_, _) => Changed();
                    watcher.Error += (_, _) => Changed();
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Warning,
                        $"Proxy '{_proxyName}': the certificate file '{path}' cannot be watched ({ex.Message}). It is checked every {_pollTime.TotalSeconds:0} seconds instead.");
                }
            }

            _settleTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
            _pollTimer = new Timer(_ => Poll(), null, _pollTime, _pollTime);
        }

        private void Changed()
        {
            lock (_reloadLock)
            {
                _retries = 0;
                _settleTimer?.Change(_settleTime, Timeout.InfiniteTimeSpan);
            }
        }

        private void Poll()
        {
            var snapshot = Volatile.Read(ref _snapshot);
            if (_sources.Any(o => Stamp(o.Path) != (snapshot.Stamps.TryGetValue(o.Path, out var known) ? known : default)))
            {
                Changed();
            }
        }

        private void Reload()
        {
            lock (_reloadLock)
            {
                if (_disposed)
                {
                    return;
                }

                var current = _snapshot;
                if (_sources.All(o => Stamp(o.Path) == (current.Stamps.TryGetValue(o.Path, out var known) ? known : default)))
                {
                    return; //Nothing changed after all.
                }

                Snapshot next;
                try
                {
                    next = Load(_sources);
                }
                catch (Exception ex)
                {
                    //The old certificates keep being used. The file may still be being written, so it is tried again for a while.
                    if (_retries++ < MaxRetries)
                    {
                        _settleTimer?.Change(_retryTime, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        Singletons.Logging.Write(NpLogging.Severity.Warning,
                            $"Proxy '{_proxyName}': a changed certificate file cannot be used, so the previous certificates stay in use. {ex.GetBaseException().Message}");
                    }
                    return;
                }

                Volatile.Write(ref _snapshot, next);

                var changed = _sources.Where(o => current.Stamps.TryGetValue(o.Path, out var old) == false || old != next.Stamps[o.Path])
                    .Select(o => o.Host ?? "(proxy)");
                Singletons.Logging.Write(NpLogging.Severity.Information,
                    $"Proxy '{_proxyName}': the TLS certificate was reloaded ({string.Join(", ", changed)}). New connections use it.");

                //Connections that are open keep the old ones in use until they end, so these are only let go of later.
                Task.Delay(_disposeDelay).ContinueWith(_ =>
                {
                    foreach (var certificate in current.All)
                    {
                        certificate.Dispose();
                    }
                });
            }
        }

        public void Dispose()
        {
            lock (_reloadLock)
            {
                _disposed = true;
            }

            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _settleTimer?.Dispose();
            _pollTimer?.Dispose();

            foreach (var certificate in _snapshot.All)
            {
                certificate.Dispose();
            }
        }
    }
}
