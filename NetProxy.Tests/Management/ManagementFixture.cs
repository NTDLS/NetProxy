using NetProxy.ClientApi;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Service;
using NetProxy.Tests.Support;
using NTDLS.Persistence;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// The service, running in this process with a configuration of its own, for the tests that talk to it the way a console does.
    /// </summary>
    public sealed class ManagementFixture : IDisposable
    {
        public const string UserName = "admin";
        public const string Password = "admin-pass";

        private readonly TempDirectory _directory = new();

        public int Port { get; }
        public string LogPath { get; }
        internal NpServiceManager Manager { get; }

        public NpConnectionOptions Options => new() { Server = "127.0.0.1", Port = Port, UserName = UserName, PasswordHash = NpUtility.Sha256(Password) };

        public ManagementFixture()
        {
            TestEnvironment.ResetConfiguration();

            Port = Net.FreePort();
            LogPath = _directory.File("service.log");

            var configuration = new NpConfiguration
            {
                ManagementPort = Port,
                Settings = new NpServiceSettings { LogPath = LogPath, LogToEventLog = false, ManagementListenOnIPv6 = false }
            };
            configuration.Users.Add(new NpUser { Id = Guid.NewGuid().ToString(), UserName = UserName, PasswordHash = NpUtility.Sha256(Password) });
            CommonApplicationData.SaveToDisk(TestEnvironment.ConfigurationName, configuration);

            Manager = new NpServiceManager();
            Manager.Start();

            //The service starts on a thread of its own in real life; here it is ready when it answers.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var client = NpServiceClient.Connect(Options);
                    client.GetInfo();
                    break;
                }
                catch when (attempt < 50)
                {
                    Thread.Sleep(100);
                }
            }
        }

        public NpServiceClient Connect() => NpServiceClient.Connect(Options);

        public void Dispose()
        {
            Manager.Stop();
            Singletons.ShutdownLogging();
            _directory.Dispose();
            TestEnvironment.ResetConfiguration();
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ManagementCollection : ICollectionFixture<ManagementFixture>
    {
        public const string Name = "Management";
    }

    /// <summary>
    /// A test class that starts from a service with no proxies, no plugins and only the first user.
    /// </summary>
    public abstract class ManagementTest : IDisposable
    {
        protected ManagementFixture Service { get; }
        protected TestHost Host { get; } = new();
        protected NpServiceClient Client { get; }

        protected ManagementTest(ManagementFixture service)
        {
            Service = service;
            Client = service.Connect();
            Clean();
        }

        private void Clean()
        {
            foreach (var proxy in Client.ListProxies())
            {
                Client.ProxyAction(proxy.Name, NpProxyAction.Delete);
            }

            foreach (var plugin in Client.ListPlugins())
            {
                Client.PluginAction(plugin.Name, NpPluginAction.Delete);
            }

            foreach (var user in Client.ListUsers().Where(o => o.UserName != ManagementFixture.UserName))
            {
                Client.RemoveUser(user.UserName);
            }

            Client.SetSettings(new NpServiceSettings { LogPath = Service.LogPath, LogToEventLog = false, ManagementListenOnIPv6 = false });
        }

        public virtual void Dispose()
        {
            Host.StopProxies();
            Clean();
            Client.Dispose();
            Host.Dispose();
        }

        /// <summary>
        /// A proxy on a free port in front of a new backend, as the file of a user would describe it.
        /// </summary>
        protected NpProxyConfiguration NewProxy(string name, string backendName = "A", Action<NpProxyConfiguration>? change = null)
        {
            var backend = Host.Backend(backendName);
            return Support.ProxyBuilder.Configuration(name, Net.FreePort(), NetProxy.Library.TrafficType.Http, backend.Port, change);
        }

        protected async Task<string> Get(NpProxyConfiguration proxy, string host = "x")
            => await Support.Client.Http(proxy.ListenPort, host);
    }
}
