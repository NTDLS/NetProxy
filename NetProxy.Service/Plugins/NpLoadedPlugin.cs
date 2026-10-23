using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using System.Reflection;

namespace NetProxy.Service.Plugins
{
    /// <summary>
    /// A plug-in of the service: its configuration, and the instance of it when it is loaded. Calls into the plug-in go through
    /// <see cref="RunAsync{T}"/>, which keeps a plug-in that fails or is slow from affecting the service.
    /// </summary>
    internal sealed class NpLoadedPlugin : INpPluginHost
    {
        private static readonly TimeSpan _failureLogInterval = TimeSpan.FromSeconds(5);

        private NpPluginLoadContext? _context;
        private HashSet<string> _handled = [];
        private long _lastFailureLogTicks;

        public NpPluginConfiguration Configuration { get; }
        public NpPlugin? Instance { get; private set; }
        public NpPluginAttribute? Info { get; private set; }
        public string LoadError { get; private set; } = string.Empty;
        public DateTime LastTickUtc { get; set; } = DateTime.UtcNow;

        public bool IsLoaded => Instance != null;

        public NpLoadedPlugin(NpPluginConfiguration configuration)
        {
            Configuration = configuration;
        }

        //---------------------------------------------------------------- INpPluginHost

        public string InstanceName => Configuration.Name;

        public IReadOnlyDictionary<string, string> Settings => Configuration.Settings;

        public string DataDirectory
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Singletons.ConfigurationName, "Plugins", Configuration.Id.ToString("N"));
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public void Log(NpPluginLogLevel level, string message)
        {
            Singletons.Logging.Write(level switch
            {
                NpPluginLogLevel.Verbose => NpLogging.Severity.Verbose,
                NpPluginLogLevel.Information => NpLogging.Severity.Information,
                NpPluginLogLevel.Warning => NpLogging.Severity.Warning,
                _ => NpLogging.Severity.Exception
            }, $"[Plugin '{Configuration.Name}'] {message}");
        }

        //----------------------------------------------------------------

        /// <summary>
        /// True if the plug-in overrides the event, so that events nobody listens to cost nothing.
        /// </summary>
        public bool Handles(string hook) => _handled.Contains(hook);

        /// <summary>
        /// Loads the assembly and creates the plug-in. Does not throw: the reason is in <see cref="LoadError"/>.
        /// </summary>
        public void Load()
        {
            Instance = null;
            LoadError = string.Empty;

            try
            {
                var path = Path.IsPathRooted(Configuration.AssemblyPath)
                    ? Configuration.AssemblyPath : Path.Combine(AppContext.BaseDirectory, Configuration.AssemblyPath);

                if (string.IsNullOrWhiteSpace(Configuration.AssemblyPath) || File.Exists(path) == false)
                {
                    throw new FileNotFoundException($"The plug-in file '{path}' was not found.");
                }

                _context = new NpPluginLoadContext(path);
                var assembly = _context.LoadPlugin(path);

                Type type;
                if (string.IsNullOrWhiteSpace(Configuration.TypeName))
                {
                    type = assembly.GetExportedTypes().FirstOrDefault(o => o.IsAbstract == false && typeof(NpPlugin).IsAssignableFrom(o))
                        ?? throw new InvalidOperationException("The assembly has no public plug-in class (a class that derives from NpPlugin).");
                }
                else
                {
                    type = assembly.GetType(Configuration.TypeName.Trim(), throwOnError: false)
                        ?? throw new InvalidOperationException($"The class '{Configuration.TypeName}' was not found in the assembly.");
                }

                if (typeof(NpPlugin).IsAssignableFrom(type) == false || type.IsAbstract)
                {
                    throw new InvalidOperationException($"'{type.FullName}' is not a plug-in class (it has to derive from NpPlugin).");
                }

                var info = type.GetCustomAttribute<NpPluginAttribute>();
                if (info != null && info.ApiVersion > NpPluginApiInfo.Version)
                {
                    throw new InvalidOperationException(
                        $"The plug-in was written for version {info.ApiVersion} of the plug-in API, and this service has version {NpPluginApiInfo.Version}.");
                }

                var instance = (NpPlugin)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"'{type.FullName}' could not be created."));

                instance.Attach(this);

                _handled = typeof(NpPlugin).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(o => o.IsVirtual && o.Name.StartsWith("On"))
                    .Where(o => type.GetMethod(o.Name)?.DeclaringType != typeof(NpPlugin))
                    .Select(o => o.Name)
                    .ToHashSet();

                Info = info;
                Instance = instance;
            }
            catch (Exception ex)
            {
                LoadError = ex.GetBaseException().Message;
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"Plugin '{Configuration.Name}' could not be loaded: {LoadError}");
                Unload();
            }
        }

        /// <summary>
        /// Tells the plug-in that the service has started and it can set up. A plug-in that fails to is not used.
        /// </summary>
        public async Task StartAsync()
        {
            if (Instance == null)
            {
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1000, Configuration.TimeoutMilliseconds * 4)));
                await Instance.OnServiceStartingAsync(cts.Token).AsTask().WaitAsync(cts.Token);
            }
            catch (Exception ex)
            {
                LoadError = $"The plug-in failed to start: {ex.GetBaseException().Message}";
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"Plugin '{Configuration.Name}': {LoadError}");
                Unload();
            }
        }

        /// <summary>
        /// Tells the plug-in that it is going away, then lets go of its assembly.
        /// </summary>
        public async Task StopAsync()
        {
            if (Instance != null)
            {
                await RunAsync(nameof(NpPlugin.OnServiceStoppingAsync), (p, ct) => p.OnServiceStoppingAsync(ct));
            }

            Unload();
        }

        private void Unload()
        {
            Instance = null;
            _handled = [];

            try { _context?.Unload(); } catch { }
            _context = null;
        }

        /// <summary>
        /// Calls an event of the plug-in. Returns false if it threw or took too long (which is logged), in which case the plug-in
        /// is skipped for the event.
        /// </summary>
        public async ValueTask<bool> RunAsync(string hook, Func<NpPlugin, CancellationToken, ValueTask> call)
        {
            var result = await RunAsync(hook, async (p, ct) => { await call(p, ct); return true; });
            return result.Succeeded;
        }

        public async ValueTask<(bool Succeeded, T Value)> RunAsync<T>(string hook, Func<NpPlugin, CancellationToken, ValueTask<T>> call)
        {
            var instance = Instance;
            if (instance == null)
            {
                return (false, default!);
            }

            var timeout = TimeSpan.FromMilliseconds(Math.Max(10, Configuration.TimeoutMilliseconds));
            var cts = new CancellationTokenSource(timeout);

            try
            {
                var pending = call(instance, cts.Token);

                if (pending.IsCompletedSuccessfully)
                {
                    var value = pending.Result;
                    cts.Dispose();
                    return (true, value);
                }

                var result = await pending.AsTask().WaitAsync(timeout);
                cts.Dispose();
                return (true, result);
            }
            catch (TimeoutException)
            {
                cts.Cancel(); //Whatever the plug-in is still doing is told to stop. It is not disposed: the plug-in may still be using it.
                LogFailure(hook, $"took longer than {timeout.TotalMilliseconds:n0} ms");
                return (false, default!);
            }
            catch (Exception ex)
            {
                cts.Dispose();
                LogFailure(hook, ex.GetBaseException().Message);
                return (false, default!);
            }
        }

        private void LogFailure(string hook, string problem)
        {
            //Events can come with every read of every connection, so a plug-in that fails would flood the log.
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastFailureLogTicks);
            if (now - last >= _failureLogInterval.Ticks && Interlocked.CompareExchange(ref _lastFailureLogTicks, now, last) == last)
            {
                Singletons.Logging.Write(NpLogging.Severity.Warning, $"Plugin '{Configuration.Name}' failed in {hook}: {problem}.");
            }
        }
    }
}
