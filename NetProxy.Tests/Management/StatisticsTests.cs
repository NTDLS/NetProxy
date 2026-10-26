using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Service;
using NetProxy.Service.Proxy;
using NetProxy.Tests.Support;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// The statistics of the proxies: kept across edits and restarts of the service, and cleared on request.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class StatisticsTests(ManagementFixture service) : ManagementTest(service)
    {
        private static string StatisticsFile => Path.Combine(TestEnvironment.ConfigurationFolder, "NpProxyStatisticsRecord.json");

        private async Task<Guid> ProxyWithTraffic(string name, int requests)
        {
            var proxy = NewProxy(name);
            Assert.True(Client.ApplyProxy(proxy).Success);

            for (var i = 0; i < requests; i++)
            {
                await Get(proxy);
            }

            return Client.ListProxies().Single(o => o.Name == name).Id;
        }

        private NetProxy.Library.Payloads.NpProxyGridStats Stats(Guid id)
            => Client.GetStatistics()[id];

        [Fact]
        public async Task Clearing_the_statistics_sets_the_counters_of_that_proxy_to_zero_and_they_count_again()
        {
            var id = await ProxyWithTraffic("counted", 3);
            var other = await ProxyWithTraffic("other", 2);

            await NetProxy.Tests.Support.Client.Eventually(() => Task.FromResult(Stats(id)), o => o.TotalConnections == 3 && o.BytesRead > 0 && o.BytesWritten > 0);

            Assert.Null(Client.ProxyAction("counted", NpProxyAction.ClearStatistics));

            var cleared = Stats(id);
            Assert.Equal(0UL, cleared.TotalConnections);
            Assert.Equal(0UL, cleared.BytesRead);
            Assert.Equal(0UL, cleared.BytesWritten);
            Assert.Equal(2UL, Stats(other).TotalConnections); //The others keep theirs.

            await Get(Client.GetProxy("counted")!);
            Assert.Equal(1UL, Stats(id).TotalConnections);
        }

        [Fact]
        public void Clearing_the_statistics_of_a_proxy_that_does_not_exist_says_so()
        {
            var problem = Client.ProxyAction("nobody", NpProxyAction.ClearStatistics);

            Assert.NotNull(problem);
        }

        [Fact]
        public async Task The_statistics_are_written_to_a_file_and_clearing_them_is_written_too()
        {
            var id = await ProxyWithTraffic("filed", 2);

            Singletons.Statistics.Save();

            var record = JArray.Parse(File.ReadAllText(StatisticsFile)).OfType<JObject>().Single(o => (string?)o["ProxyId"] == id.ToString());
            Assert.Equal(2UL, (ulong)record["TotalConnections"]!);
            Assert.True((ulong)record["BytesRead"]! > 0);

            Client.ProxyAction("filed", NpProxyAction.ClearStatistics);

            record = JArray.Parse(File.ReadAllText(StatisticsFile)).OfType<JObject>().Single(o => (string?)o["ProxyId"] == id.ToString());
            Assert.Equal(0UL, (ulong)record["TotalConnections"]!);
            Assert.Equal(0UL, (ulong)record["BytesRead"]!);
        }

        [Fact]
        public async Task A_proxy_that_is_edited_carries_on_with_its_numbers()
        {
            var id = await ProxyWithTraffic("edited", 2);

            var proxy = Client.GetProxy("edited")!;
            proxy.Description = "changed, which replaces the running proxy";
            var before = Service.Manager.FindProxy(null, "edited");
            Assert.True(Client.ApplyProxy(proxy).Success);

            Assert.NotSame(before, Service.Manager.FindProxy(null, "edited"));
            Assert.Equal(2UL, Stats(id).TotalConnections);
        }

        [Fact]
        public async Task The_numbers_come_back_when_the_service_starts_again()
        {
            var id = await ProxyWithTraffic("restarted", 2);
            var bytes = Stats(id).BytesRead;
            Singletons.Statistics.Save();

            //What a start of the service does before it makes its proxies.
            Singletons.Statistics.Load();
            var again = new NpProxy(Client.GetProxy("restarted")!);

            Assert.Equal(2UL, again.Statistics.TotalConnections);
            Assert.Equal(bytes, again.Statistics.BytesRead);
            Assert.Equal(0, again.Statistics.CurrentConnections);
        }

        [Fact]
        public async Task The_endpoints_keep_their_numbers_too()
        {
            var id = await ProxyWithTraffic("endpoints", 2);
            var running = Service.Manager.FindProxy(null, "endpoints")!;
            var endpointId = running.Configuration.Endpoints.Collection.Single().Id;
            Assert.Equal(2UL, running.EndpointStatistics[endpointId].TotalConnections);
            Singletons.Statistics.Save();

            Singletons.Statistics.Load();
            var again = new NpProxy(Client.GetProxy("endpoints")!);

            Assert.Equal(2UL, again.EndpointStatistics[endpointId].TotalConnections);

            Client.ProxyAction("endpoints", NpProxyAction.ClearStatistics);
            Assert.Equal(0UL, running.EndpointStatistics[endpointId].TotalConnections);
            Assert.NotEqual(Guid.Empty, id);
        }
    }
}
