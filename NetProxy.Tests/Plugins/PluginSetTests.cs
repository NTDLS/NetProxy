using NetProxy.PluginApi;
using NetProxy.Service.Plugins;
using NetProxy.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Plugins
{
    /// <summary>
    /// The plugins that apply to a proxy, called in order.
    /// </summary>
    public class PluginSetTests
    {
        private static async Task<NpPluginSet> Set(TempDirectory directory, params (string Name, Dictionary<string, string>? Settings)[] plugins)
        {
            var loaded = new List<NpLoadedPlugin>();
            foreach (var (name, settings) in plugins)
            {
                loaded.Add(await PluginSupport.Load(PluginSupport.Recorder(name, directory, settings)));
            }

            return new NpPluginSet(loaded);
        }

        private static NpDataEventArgs Data(string text, FakeConnection? connection = null, NpDataDirection direction = NpDataDirection.ClientToEndpoint)
            => new(connection ?? new FakeConnection(), direction, Encoding.UTF8.GetBytes(text), receivedEncrypted: true, willBeSentEncrypted: false);

        [Fact]
        public async Task An_empty_set_does_nothing_and_allows_everything()
        {
            var connection = new FakeConnection();

            Assert.True(NpPluginSet.Empty.IsEmpty);
            Assert.True((await NpPluginSet.Empty.ConnectingAsync(connection)).Allowed);
            Assert.False(NpPluginSet.Empty.HasDataHooks);
            await NpPluginSet.Empty.DataReceivedAsync(Data("x"));
        }

        [Fact]
        public async Task Events_are_given_to_the_plugins_in_order()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("first", new() { ["Tag"] = "1" }), ("second", new() { ["Tag"] = "2" }));

            await set.ConnectedAsync(new FakeConnection());

            Assert.Contains(PluginSupport.Events(directory, "first"), o => o.StartsWith("1:Connected"));
            Assert.Contains(PluginSupport.Events(directory, "second"), o => o.StartsWith("2:Connected"));
        }

        [Fact]
        public async Task The_first_plugin_to_say_no_ends_it_and_its_name_is_in_the_answer()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory,
                ("allows", null),
                ("blocks", new() { ["DenyClients"] = "203.0.113.5" }),
                ("never-asked", null));

            var verdict = await set.ConnectingAsync(new FakeConnection());

            Assert.False(verdict.Allowed);
            Assert.Contains("blocks", verdict.Reason);
            Assert.Contains("blocked", verdict.Reason);
            Assert.Contains(PluginSupport.Events(directory, "allows"), o => o.StartsWith("Connecting"));
            Assert.DoesNotContain(PluginSupport.Events(directory, "never-asked"), o => o.StartsWith("Connecting"));
        }

        [Fact]
        public async Task A_plugin_that_throws_cannot_say_no_and_does_not_stop_the_others()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("throws", new() { ["Throw"] = "true", ["DenyClients"] = "203.0.113.5" }), ("after", null));

            var verdict = await set.ConnectingAsync(new FakeConnection());
            await set.DataReceivedAsync(Data("hello"));

            Assert.True(verdict.Allowed); //The deny was recorded before it threw only for data events; Connecting does not throw.
            Assert.Contains(PluginSupport.Events(directory, "after"), o => o.StartsWith("DataReceived"));
        }

        [Fact]
        public async Task A_plugin_that_is_too_slow_is_skipped_and_the_others_still_run()
        {
            using var directory = new TempDirectory();
            var slow = await PluginSupport.Load(PluginSupport.Recorder("slow", directory, new() { ["SleepMs"] = "5000" }, timeoutMilliseconds: 200));
            var fast = await PluginSupport.Load(PluginSupport.Recorder("fast", directory));
            var set = new NpPluginSet([slow, fast]);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await set.DataReceivedAsync(Data("hello"));

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
            Assert.Contains(PluginSupport.Events(directory, "fast"), o => o.StartsWith("DataReceived"));
        }

        [Fact]
        public async Task Data_changed_by_one_plugin_is_what_the_next_one_gets()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("changes", new() { ["Replace"] = "cat=>dog" }), ("sees", null));
            var data = Data("a cat sat");

            await set.DataSendingAsync(data);

            Assert.True(data.IsModified);
            Assert.Equal("a dog sat", Encoding.UTF8.GetString(data.Data.Span));
            Assert.Contains("a dog sat", string.Join("\n", PluginSupport.Events(directory, "sees")));
        }

        [Fact]
        public async Task Data_that_is_dropped_goes_no_further()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("drops", new() { ["DropOnData"] = "secret" }), ("never", null));
            var data = Data("a secret");

            await set.DataReceivedAsync(data);

            Assert.True(data.IsDropped);
            Assert.True(data.Data.IsEmpty);
            Assert.DoesNotContain(PluginSupport.Events(directory, "never"), o => o.StartsWith("DataReceived"));
        }

        [Fact]
        public async Task A_plugin_can_ask_for_the_connection_to_be_closed()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("closes", new() { ["CloseOnData"] = "bye" }));
            var data = Data("say bye");

            await set.DataReceivedAsync(data);

            Assert.Equal("matched", data.CloseReason);
            Assert.True(data.IsDropped);
        }

        [Fact]
        public async Task Data_hooks_are_only_reported_when_a_plugin_has_them()
        {
            using var directory = new TempDirectory();
            var recorder = await PluginSupport.Load(PluginSupport.Recorder("rec", directory));
            var quiet = await PluginSupport.Load(PluginSupport.Recorder("quiet", directory, typeName: nameof(QuietPlugin)));

            Assert.True(new NpPluginSet([recorder]).HasDataHooks);
            Assert.True(new NpPluginSet([recorder]).HasDataSentHook);
            Assert.False(new NpPluginSet([quiet]).HasDataHooks);
            Assert.False(new NpPluginSet([quiet]).HasDataSentHook);
        }

        [Fact]
        public async Task The_data_event_says_whether_the_data_was_and_will_be_encrypted()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("rec", null));

            await set.DataReceivedAsync(Data("x"));

            Assert.Contains("enc=True->False", PluginSupport.Events(directory, "rec").Last());
        }

        [Fact]
        public void Replacing_copies_the_data_so_the_plugin_may_reuse_its_buffer()
        {
            var data = Data("abc");
            var buffer = Encoding.UTF8.GetBytes("new");

            data.Replace(buffer);
            buffer[0] = (byte)'X';

            Assert.Equal("new", Encoding.UTF8.GetString(data.Data.Span));
        }

        [Fact]
        public async Task Events_of_the_connection_carry_what_the_plugin_needs()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("rec", null));

            await set.ConnectingAsync(new FakeConnection { HostName = "www.example.com" });
            await set.DisconnectedAsync(new FakeConnection(), new NpDisconnectInfo(NpDisconnectReason.IdleTimeout, TimeSpan.FromSeconds(1), 10, 20));

            var events = PluginSupport.Events(directory, "rec");
            Assert.Contains("Connecting 203.0.113.5 proxy=fake host=www.example.com listen=True", events);
            Assert.Contains("Disconnected reason=IdleTimeout in=10 out=20", events);
        }

        [Fact]
        public async Task The_other_events_reach_the_plugin()
        {
            using var directory = new TempDirectory();
            var set = await Set(directory, ("rec", null));
            var proxy = new NpProxyInfo(Guid.NewGuid(), "web", 443, NpTrafficKind.Https);
            var endpoint = new NpEndpointInfo(Guid.NewGuid(), "10.0.0.1", 8080, false);
            var connection = new FakeConnection();

            await set.ProxyStartedAsync(proxy, ["a", "b"]);
            await set.ProxyStartFailedAsync(proxy, "port in use");
            await set.ProxyStoppingAsync(proxy);
            await set.ProxyStoppedAsync(proxy);
            await set.ProxyConfigurationChangedAsync(proxy, NpChangeKind.Added);
            await set.ConnectionRefusedAsync(proxy, "198.51.100.1", "limit");
            await set.ClientTlsEstablishedAsync(connection);
            await set.EndpointConnectedAsync(connection, endpoint);
            await set.EndpointConnectFailedAsync(connection, endpoint, "refused");
            await set.IdleTimeoutAsync(connection);
            await set.DisconnectingAsync(connection, NpDisconnectReason.Completed);
            await set.DataSentAsync(connection, NpDataDirection.EndpointToClient, 42);
            await set.ManagementLoginAsync(new NpLoginInfo("bob", false));
            await set.ErrorAsync(new NpPluginError("it broke", null, proxy, connection));

            var events = string.Join("\n", PluginSupport.Events(directory, "rec"));
            foreach (var expected in new[]
            {
                "ProxyStarted web on=2", "ProxyStartFailed web port in use", "ProxyStopping web", "ProxyStopped web", "ProxyChanged web Added",
                "Refused 198.51.100.1", "ClientTls", "EndpointConnected 8080", "EndpointFailed 8080", "Idle", "Disconnecting reason=Completed",
                "DataSent EndpointToClient 42", "Login bob False", "Error it broke"
            })
            {
                Assert.Contains(expected, events);
            }
        }
    }
}
