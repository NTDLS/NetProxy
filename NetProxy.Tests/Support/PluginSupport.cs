using NetProxy.Library.Payloads.Routing;
using NetProxy.PluginApi;
using NetProxy.Service.Plugins;

namespace NetProxy.Tests.Support
{
    internal static class PluginSupport
    {
        /// <summary>
        /// The assembly that the test plugins are in.
        /// </summary>
        public static string AssemblyPath => typeof(RecordingPlugin).Assembly.Location;

        public static NpPluginConfiguration Recorder(string name, TempDirectory directory, Dictionary<string, string>? settings = null,
            int timeoutMilliseconds = 5000, string typeName = nameof(RecordingPlugin))
        {
            var configuration = new NpPluginConfiguration
            {
                Name = name,
                AssemblyPath = AssemblyPath,
                TypeName = typeName,
                TimeoutMilliseconds = timeoutMilliseconds
            };

            configuration.Settings["LogFile"] = LogOf(directory, name);
            foreach (var setting in settings ?? [])
            {
                configuration.Settings[setting.Key] = setting.Value;
            }

            return configuration;
        }

        public static string LogOf(TempDirectory directory, string name) => directory.File(name + ".log");

        public static List<string> Events(TempDirectory directory, string name)
        {
            var path = LogOf(directory, name);
            if (File.Exists(path) == false)
            {
                return [];
            }

            //The plugin may be writing while this reads, so the file is opened in a way that lets it.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        /// <summary>
        /// A plugin that is loaded and started, the way the service does it.
        /// </summary>
        public static async Task<NpLoadedPlugin> Load(NpPluginConfiguration configuration)
        {
            var plugin = new NpLoadedPlugin(configuration);
            plugin.Load();
            await plugin.StartAsync();
            return plugin;
        }
    }

    /// <summary>
    /// A connection, for calling the events of a plugin directly.
    /// </summary>
    public sealed class FakeConnection : INpConnection
    {
        public Guid Id { get; } = Guid.NewGuid();
        public NpProxyInfo Proxy { get; init; } = new(Guid.NewGuid(), "fake", 443, NpTrafficKind.Http);
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public string ClientAddress { get; init; } = "203.0.113.5";
        public int ClientPort => 50000;
        public string ListenAddress => "127.0.0.1";
        public int ListenPort => 443;
        public string? HostName { get; init; }
        public string? ClientTls => null;
        public string? EndpointTls => null;
        public bool IsHttp2 => false;
        public NpEndpointInfo? Endpoint { get; init; }
        public long BytesFromClient => 0;
        public long BytesFromEndpoint => 0;
        public IDictionary<string, object?> State { get; } = new Dictionary<string, object?>();
        public string? ClosedFor { get; private set; }

        public void Close(string reason) => ClosedFor = reason;
    }
}
