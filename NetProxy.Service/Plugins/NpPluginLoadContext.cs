using System.Reflection;
using System.Runtime.Loader;

namespace NetProxy.Service.Plugins
{
    /// <summary>
    /// Loads a plug-in and what it depends on apart from the service, so that it can be unloaded and loaded again. The plug-in API
    /// itself comes from the service, so that the plug-in and the service agree on its types.
    /// </summary>
    internal sealed class NpPluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public NpPluginLoadContext(string pluginPath) : base($"NetProxyPlugin:{Path.GetFileName(pluginPath)}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        /// <summary>
        /// Loads the plug-in assembly from memory, so that the file is not locked and a new build of it can be put in its place.
        /// </summary>
        public Assembly LoadPlugin(string path)
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return LoadFromStream(stream);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            //The service has these, and the plug-in has to use the same ones.
            if (assemblyName.Name is "NetProxy.PluginApi" or "NetProxy.Library")
            {
                return null;
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
