#pragma warning disable xUnit1031 //The plug-in events complete at once; there is nothing to deadlock on.
using NetProxy.PluginApi;

namespace NetProxy.Plugins.Tests.Support
{
    /// <summary>
    /// Starts plug-ins the way the service does (attached to a host, then OnServiceStarting), with a data folder of their own that is
    /// removed afterwards, and sends them events.
    /// </summary>
    public abstract class PluginTestBase : IDisposable
    {
        private readonly List<string> _directories = [];
        private readonly List<NpPlugin> _started = [];

        internal FakeClock Clock { get; } = new();

        public virtual void Dispose()
        {
            foreach (var plugin in _started)
            {
                try { plugin.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait(2000); } catch { }
            }

            foreach (var directory in _directories.Where(Directory.Exists))
            {
                try { Directory.Delete(directory, true); } catch { }
            }
        }

        internal string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "netproxy-plugins-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _directories.Add(directory);
            return directory;
        }

        internal (T Plugin, FakeHost Host) Start<T>(T plugin, Dictionary<string, string>? settings = null, string? directory = null) where T : NpPlugin
        {
            var host = new FakeHost(settings, directory ?? TempDirectory());
            Directory.CreateDirectory(host.DataDirectory);
            plugin.Attach(host);
            plugin.OnServiceStartingAsync(CancellationToken.None).AsTask().Wait();
            _started.Add(plugin);
            return (plugin, host);
        }

        internal static NpDataEventArgs Send(NpPlugin plugin, FakeConnection connection, byte[] bytes, bool canRespond = true,
            NpDataDirection direction = NpDataDirection.ClientToEndpoint)
        {
            var data = new NpDataEventArgs(connection, direction, bytes, true, false, canRespond && direction == NpDataDirection.ClientToEndpoint);
            plugin.OnDataReceivedAsync(data, CancellationToken.None).AsTask().Wait();
            return data;
        }

        internal static NpDataEventArgs Sending(NpPlugin plugin, FakeConnection connection, byte[] bytes)
        {
            var data = new NpDataEventArgs(connection, NpDataDirection.EndpointToClient, bytes, false, true);
            plugin.OnDataSendingAsync(data, CancellationToken.None).AsTask().Wait();
            return data;
        }

        internal static NpVerdict Connect(NpPlugin plugin, FakeConnection connection)
            => plugin.OnConnectingAsync(connection, CancellationToken.None).AsTask().Result;

        /// <summary>The head and the body of a response, as text.</summary>
        internal static (string Head, string Body) Split(ReadOnlyMemory<byte>? response)
        {
            var bytes = response!.Value.ToArray();
            var split = System.Text.Encoding.Latin1.GetString(bytes).IndexOf("\r\n\r\n", StringComparison.Ordinal);
            return (System.Text.Encoding.Latin1.GetString(bytes, 0, split), System.Text.Encoding.UTF8.GetString(bytes, split + 4, bytes.Length - split - 4));
        }
    }
}
