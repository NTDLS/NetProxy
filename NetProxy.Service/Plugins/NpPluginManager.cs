using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.PluginApi;
using NTDLS.Persistence;

namespace NetProxy.Service.Plugins
{
    /// <summary>
    /// The plug-ins of the service, in the order that they are called in. They are kept in their own configuration file.
    /// A proxy opts in to the plug-ins it wants (<see cref="NpProxyConfiguration.PluginIds"/>).
    /// </summary>
    internal sealed class NpPluginManager
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _listLock = new();
        private List<NpLoadedPlugin> _plugins = [];
        private CancellationTokenSource? _tickStop;
        private Task? _tickTask;

        /// <summary>
        /// Loads the plug-ins of the configuration file (the ones that are enabled) and tells them that the service has started.
        /// </summary>
        public async Task StartAsync()
        {
            await _gate.WaitAsync();
            try
            {
                var configurations = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, new List<NpPluginConfiguration>());

                var loaded = new List<NpLoadedPlugin>();
                foreach (var configuration in configurations)
                {
                    loaded.Add(await CreateAsync(configuration));
                }

                lock (_listLock)
                {
                    _plugins = loaded;
                }

                if (loaded.Count > 0)
                {
                    Singletons.Logging.Write(NpLogging.Severity.Information,
                        $"Loaded {loaded.Count(o => o.IsLoaded)} of {loaded.Count} plugins.");
                }
            }
            finally
            {
                _gate.Release();
            }

            _tickStop = new CancellationTokenSource();
            _tickTask = Task.Run(() => TickLoopAsync(_tickStop.Token));
        }

        /// <summary>
        /// Tells the plug-ins that the service is stopping and lets them go.
        /// </summary>
        public async Task StopAsync()
        {
            try { _tickStop?.Cancel(); } catch { }
            try { if (_tickTask != null) { await _tickTask.WaitAsync(TimeSpan.FromSeconds(5)); } } catch { }

            await _gate.WaitAsync();
            try
            {
                NpLoadedPlugin[] plugins;
                lock (_listLock)
                {
                    plugins = [.. _plugins];
                }

                foreach (var plugin in plugins)
                {
                    await plugin.StopAsync();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private static async Task<NpLoadedPlugin> CreateAsync(NpPluginConfiguration configuration)
        {
            var plugin = new NpLoadedPlugin(configuration);

            if (configuration.Enabled)
            {
                plugin.Load();
                await plugin.StartAsync();
            }

            return plugin;
        }

        private void Save()
        {
            try
            {
                List<NpPluginConfiguration> configurations;
                lock (_listLock)
                {
                    configurations = [.. _plugins.Select(o => o.Configuration)];
                }

                CommonApplicationData.SaveToDisk(Singletons.ConfigurationName, configurations);
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to save the plugins", ex);
            }
        }

        private async Task TickLoopAsync(CancellationToken stop)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            try
            {
                while (await timer.WaitForNextTickAsync(stop))
                {
                    NpLoadedPlugin[] plugins;
                    lock (_listLock)
                    {
                        plugins = [.. _plugins];
                    }

                    foreach (var plugin in plugins)
                    {
                        var interval = plugin.Instance?.TickInterval;
                        if (interval != null && plugin.Handles(nameof(NpPlugin.OnTickAsync)) && DateTime.UtcNow - plugin.LastTickUtc >= interval)
                        {
                            plugin.LastTickUtc = DateTime.UtcNow;
                            _ = new NpPluginSet([plugin]).TickAsync(plugin).AsTask(); //A slow plug-in does not hold the others up.
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //The service is stopping.
            }
        }

        //---------------------------------------------------------------- Who gets called

        /// <summary>
        /// The plug-ins that are loaded, for the events of the service.
        /// </summary>
        public NpPluginSet ForService()
        {
            lock (_listLock)
            {
                return _plugins.Count == 0 ? NpPluginSet.Empty : new NpPluginSet(_plugins.Where(o => o.IsLoaded));
            }
        }

        /// <summary>
        /// The plug-ins that are loaded and that the proxy has opted in to, in the order of the service configuration.
        /// </summary>
        public NpPluginSet ForProxy(NpProxyConfiguration proxy)
        {
            if (proxy.PluginIds.Count == 0)
            {
                return NpPluginSet.Empty;
            }

            lock (_listLock)
            {
                return new NpPluginSet(_plugins.Where(o => o.IsLoaded && proxy.PluginIds.Contains(o.Configuration.Id)));
            }
        }

        //---------------------------------------------------------------- Management

        public List<NpPluginGridItem> GetGridItems()
        {
            lock (_listLock)
            {
                return _plugins.Select(o => new NpPluginGridItem
                {
                    Id = o.Configuration.Id,
                    Name = o.Configuration.Name,
                    Description = o.Configuration.Description,
                    Enabled = o.Configuration.Enabled,
                    AssemblyPath = o.Configuration.AssemblyPath,
                    TypeName = o.Configuration.TypeName,
                    Settings = new Dictionary<string, string>(o.Configuration.Settings),
                    TimeoutMilliseconds = o.Configuration.TimeoutMilliseconds,
                    IsLoaded = o.IsLoaded,
                    LoadError = o.LoadError,
                    PluginName = o.Info?.Name ?? string.Empty,
                    PluginVersion = o.Info?.Version ?? string.Empty,
                    PluginDescription = o.Info?.Description ?? string.Empty,
                    PluginAuthor = o.Info?.Author ?? string.Empty
                }).ToList();
            }
        }

        /// <summary>
        /// Adds a plug-in (at the end of the list) or changes it, and loads it again. Returns why it was not accepted, or null.
        /// </summary>
        public async Task<string?> UpsertAsync(NpPluginConfiguration configuration)
        {
            configuration.Name = configuration.Name.Trim();
            configuration.AssemblyPath = configuration.AssemblyPath.Trim();
            configuration.TypeName = configuration.TypeName.Trim();
            configuration.TimeoutMilliseconds = Math.Clamp(configuration.TimeoutMilliseconds, 10, 600_000);

            if (configuration.Name.Length == 0)
            {
                return "The plugin needs a name.";
            }

            if (configuration.AssemblyPath.Length == 0)
            {
                return "The plugin needs an assembly (.dll).";
            }

            await _gate.WaitAsync();
            try
            {
                NpLoadedPlugin? existing;
                lock (_listLock)
                {
                    if (_plugins.Any(o => o.Configuration.Id != configuration.Id && o.Configuration.Name.Equals(configuration.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        return $"There is already a plugin named '{configuration.Name}'.";
                    }

                    existing = _plugins.FirstOrDefault(o => o.Configuration.Id == configuration.Id);
                }

                if (existing != null)
                {
                    await existing.StopAsync();
                }

                var plugin = await CreateAsync(configuration);

                lock (_listLock)
                {
                    var index = existing != null ? _plugins.IndexOf(existing) : -1;
                    if (index >= 0)
                    {
                        _plugins[index] = plugin;
                    }
                    else
                    {
                        _plugins.Add(plugin);
                    }
                }

                Save();
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Unloads and removes a plug-in. The proxies that used it are not changed here.
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            await _gate.WaitAsync();
            try
            {
                NpLoadedPlugin? plugin;
                lock (_listLock)
                {
                    plugin = _plugins.FirstOrDefault(o => o.Configuration.Id == id);
                    if (plugin == null)
                    {
                        return false;
                    }

                    _plugins.Remove(plugin);
                }

                await plugin.StopAsync();
                Save();
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task MoveAsync(Guid id, int direction)
        {
            await _gate.WaitAsync();
            try
            {
                lock (_listLock)
                {
                    var index = _plugins.FindIndex(o => o.Configuration.Id == id);
                    var target = index + Math.Sign(direction);

                    if (index < 0 || target < 0 || target >= _plugins.Count)
                    {
                        return;
                    }

                    (_plugins[index], _plugins[target]) = (_plugins[target], _plugins[index]);
                }

                Save();
            }
            finally
            {
                _gate.Release();
            }
        }

        public sealed class FileReloadResult
        {
            public List<string> Changes { get; } = [];
            public List<string> Problems { get; } = [];
        }

        /// <summary>
        /// Applies what was changed in the plugin file by hand: plugins that were added, changed (they are loaded again), removed,
        /// and the order. With <paramref name="dryRun"/> it only says what it would do.
        /// </summary>
        public async Task<FileReloadResult> ReloadFromDiskAsync(bool dryRun)
        {
            var result = new FileReloadResult();

            List<NpPluginConfiguration> desired;
            try
            {
                desired = CommonApplicationData.LoadFromDisk(Singletons.ConfigurationName, new List<NpPluginConfiguration>());
            }
            catch (Exception ex)
            {
                result.Problems.Add($"The plugin file could not be read: {ex.GetBaseException().Message}");
                return result;
            }

            await _gate.WaitAsync();
            try
            {
                NpLoadedPlugin[] current;
                lock (_listLock)
                {
                    current = [.. _plugins];
                }

                //A file with no plugins is how a file that could not be understood looks. Deleting every plugin is done on purpose.
                if (desired.Count == 0 && current.Length > 0)
                {
                    result.Problems.Add("The plugin file has no plugins, which is probably a mistake (delete the plugins with the plugin remove command if you mean it).");
                    return result;
                }

                var matches = new Dictionary<NpPluginConfiguration, NpLoadedPlugin>();
                var taken = new HashSet<NpLoadedPlugin>();

                foreach (var plugin in desired)
                {
                    var existing = current.FirstOrDefault(o => o.Configuration.Id == plugin.Id && taken.Contains(o) == false);
                    if (existing != null)
                    {
                        matches[plugin] = existing;
                        taken.Add(existing);
                    }
                }

                foreach (var plugin in desired.Where(o => matches.ContainsKey(o) == false))
                {
                    var existing = current.FirstOrDefault(o => taken.Contains(o) == false
                        && o.Configuration.Name.Equals(plugin.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (existing != null)
                    {
                        plugin.Id = existing.Configuration.Id;
                        matches[plugin] = existing;
                        taken.Add(existing);
                    }
                }

                foreach (var plugin in desired)
                {
                    if (string.IsNullOrWhiteSpace(plugin.Name) || string.IsNullOrWhiteSpace(plugin.AssemblyPath))
                    {
                        result.Problems.Add($"Plugin '{plugin.Name}': it needs a name and an assembly.");
                    }
                    else if (desired.Count(o => o.Name.Equals(plugin.Name, StringComparison.CurrentCultureIgnoreCase)) > 1)
                    {
                        result.Problems.Add($"There is more than one plugin named '{plugin.Name}'.");
                    }
                }

                if (result.Problems.Count > 0)
                {
                    return result;
                }

                var removed = current.Where(o => taken.Contains(o) == false).ToList();
                var added = desired.Where(o => matches.ContainsKey(o) == false).ToList();
                var changed = desired.Where(o => matches.TryGetValue(o, out var existing)
                    && Newtonsoft.Json.JsonConvert.SerializeObject(existing.Configuration) != Newtonsoft.Json.JsonConvert.SerializeObject(o)).ToList();
                var reordered = removed.Count == 0 && added.Count == 0
                    && desired.Select(o => o.Id).SequenceEqual(current.Select(o => o.Configuration.Id)) == false;

                result.Changes.AddRange(removed.Select(o => $"Plugin '{o.Configuration.Name}': removed."));
                result.Changes.AddRange(changed.Select(o => $"Plugin '{o.Name}': changed."));
                result.Changes.AddRange(added.Select(o => $"Plugin '{o.Name}': added."));
                if (reordered)
                {
                    result.Changes.Add("Plugins: new order.");
                }

                if (dryRun)
                {
                    return result;
                }

                foreach (var plugin in removed)
                {
                    await plugin.StopAsync();
                }

                var replacements = new Dictionary<NpLoadedPlugin, NpLoadedPlugin>();
                foreach (var plugin in changed)
                {
                    var existing = matches[plugin];
                    await existing.StopAsync();
                    replacements[existing] = await CreateAsync(plugin);
                }

                var created = new Dictionary<Guid, NpLoadedPlugin>();
                foreach (var plugin in added)
                {
                    created[plugin.Id] = await CreateAsync(plugin);
                }

                lock (_listLock)
                {
                    var next = new List<NpLoadedPlugin>();
                    foreach (var plugin in desired)
                    {
                        if (created.TryGetValue(plugin.Id, out var fresh))
                        {
                            next.Add(fresh);
                        }
                        else
                        {
                            var existing = matches[plugin];
                            next.Add(replacements.TryGetValue(existing, out var replacement) ? replacement : existing);
                        }
                    }

                    _plugins = next;
                }

                Save();
                return result;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> ReloadAsync(Guid id)
        {
            NpPluginConfiguration? configuration;
            lock (_listLock)
            {
                configuration = _plugins.FirstOrDefault(o => o.Configuration.Id == id)?.Configuration;
            }

            if (configuration == null)
            {
                return false;
            }

            return await UpsertAsync(configuration) == null;
        }
    }
}
