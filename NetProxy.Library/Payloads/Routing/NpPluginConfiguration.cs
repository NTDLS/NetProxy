namespace NetProxy.Library.Payloads.Routing
{
    /// <summary>
    /// A plug-in that the service loads. Plug-ins are set up for the service; a proxy then opts in to the ones it wants
    /// (<see cref="NpProxyConfiguration.PluginIds"/>). The order of the plug-ins in the service configuration is the order they are called in.
    /// </summary>
    public class NpPluginConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The name of this plug-in in the console. Not the name that the plug-in has for itself.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// A disabled plug-in is not loaded, and is not called for any proxy.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The plug-in assembly (.dll), on the machine that the service runs on. A relative path is relative to the folder of the service.
        /// </summary>
        public string AssemblyPath { get; set; } = string.Empty;

        /// <summary>
        /// The full name of the plug-in class. Empty to use the first plug-in class in the assembly.
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// Name and value pairs that the plug-in reads. Stored as they are typed: do not keep secrets here.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; } = new();

        /// <summary>
        /// How long an event may take before the plug-in is skipped for it, in milliseconds.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = Constants.DefaultPluginTimeoutMilliseconds;
    }

    /// <summary>
    /// A plug-in as the console lists it: its configuration and whether it loaded.
    /// </summary>
    public class NpPluginGridItem : NpPluginConfiguration
    {
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Why the plug-in is not loaded, if it is enabled and failed to load.
        /// </summary>
        public string LoadError { get; set; } = string.Empty;

        /// <summary>
        /// What the plug-in says about itself.
        /// </summary>
        public string PluginName { get; set; } = string.Empty;
        public string PluginVersion { get; set; } = string.Empty;
        public string PluginDescription { get; set; } = string.Empty;
        public string PluginAuthor { get; set; } = string.Empty;
    }
}
