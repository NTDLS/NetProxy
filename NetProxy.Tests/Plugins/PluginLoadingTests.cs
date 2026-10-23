using NetProxy.PluginApi;
using NetProxy.Service.Plugins;
using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Plugins
{
    public class PluginLoadingTests
    {
        [Fact]
        public async Task A_plugin_is_loaded_started_and_describes_itself()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("rec", directory));

            Assert.True(plugin.IsLoaded);
            Assert.Equal("", plugin.LoadError);
            Assert.Equal("Recorder", plugin.Info!.Name);
            Assert.Equal("1.2", plugin.Info.Version);
            Assert.Equal("tests", plugin.Info.Author);
            Assert.Contains("ServiceStarting name=rec dir=True", PluginSupport.Events(directory, "rec").Single());
        }

        [Fact]
        public async Task The_plugin_gets_its_settings_and_a_folder_of_its_own()
        {
            using var directory = new TempDirectory();
            var configuration = PluginSupport.Recorder("settings", directory, new() { ["Tag"] = "T1" });

            await PluginSupport.Load(configuration);

            Assert.StartsWith("T1:ServiceStarting", PluginSupport.Events(directory, "settings").Single());
        }

        [Fact]
        public async Task Only_the_events_a_plugin_overrides_are_called()
        {
            using var directory = new TempDirectory();
            var recorder = await PluginSupport.Load(PluginSupport.Recorder("rec", directory));
            var quiet = await PluginSupport.Load(PluginSupport.Recorder("quiet", directory, typeName: nameof(QuietPlugin)));

            Assert.True(recorder.Handles(nameof(NpPlugin.OnConnectingAsync)));
            Assert.True(recorder.Handles(nameof(NpPlugin.OnDataReceivedAsync)));
            Assert.False(quiet.Handles(nameof(NpPlugin.OnConnectingAsync)));
            Assert.False(quiet.Handles(nameof(NpPlugin.OnDataReceivedAsync)));
            Assert.False(quiet.Handles(nameof(NpPlugin.OnServiceStartingAsync)));
        }

        [Fact]
        public async Task A_file_that_is_not_there_is_reported_not_thrown()
        {
            using var directory = new TempDirectory();
            var configuration = PluginSupport.Recorder("missing", directory);
            configuration.AssemblyPath = directory.File("nope.dll");

            var plugin = await PluginSupport.Load(configuration);

            Assert.False(plugin.IsLoaded);
            Assert.Contains("nope.dll", plugin.LoadError);
        }

        [Fact]
        public async Task A_file_that_is_not_an_assembly_is_reported()
        {
            using var directory = new TempDirectory();
            var configuration = PluginSupport.Recorder("junk", directory);
            configuration.AssemblyPath = directory.File("junk.dll");
            File.WriteAllText(configuration.AssemblyPath, "this is not a dll");

            var plugin = await PluginSupport.Load(configuration);

            Assert.False(plugin.IsLoaded);
            Assert.NotEqual("", plugin.LoadError);
        }

        [Theory]
        [InlineData("DoesNotExist", "was not found")]
        [InlineData(nameof(NotAPlugin), "not a plug-in class")]
        [InlineData(nameof(AbstractPlugin), "not a plug-in class")]
        public async Task A_class_that_is_not_a_plugin_is_refused(string typeName, string reason)
        {
            using var directory = new TempDirectory();

            var plugin = await PluginSupport.Load(PluginSupport.Recorder("bad", directory, typeName: typeName));

            Assert.False(plugin.IsLoaded);
            Assert.Contains(reason, plugin.LoadError);
        }

        [Fact]
        public async Task A_plugin_written_for_a_newer_api_is_refused()
        {
            using var directory = new TempDirectory();

            var plugin = await PluginSupport.Load(PluginSupport.Recorder("future", directory, typeName: nameof(FuturePlugin)));

            Assert.False(plugin.IsLoaded);
            Assert.Contains("version 999", plugin.LoadError);
        }

        [Fact]
        public async Task A_plugin_that_fails_to_start_is_not_used()
        {
            using var directory = new TempDirectory();

            var plugin = await PluginSupport.Load(PluginSupport.Recorder("fails", directory, new() { ["FailOnStart"] = "1" }));

            Assert.False(plugin.IsLoaded);
            Assert.Contains("cannot start", plugin.LoadError);
        }

        [Fact]
        public async Task The_file_is_not_locked_so_a_new_build_can_be_put_in_its_place()
        {
            using var directory = new TempDirectory();
            var copy = directory.File("copy.dll");
            File.Copy(PluginSupport.AssemblyPath, copy);
            var configuration = PluginSupport.Recorder("copy", directory);
            configuration.AssemblyPath = copy;

            var plugin = await PluginSupport.Load(configuration);
            Assert.True(plugin.IsLoaded);

            File.Delete(copy);
            Assert.False(File.Exists(copy));
            Assert.True(plugin.IsLoaded);
        }

        [Fact]
        public async Task Stopping_tells_the_plugin_and_lets_go_of_it()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("stops", directory));

            await plugin.StopAsync();

            Assert.False(plugin.IsLoaded);
            Assert.Contains("ServiceStopping", PluginSupport.Events(directory, "stops"));
        }

        //---------------------------------------------------------------- Calling a plugin

        [Fact]
        public async Task A_call_that_works_says_so()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("ok", directory));
            var called = false;

            Assert.True(await plugin.RunAsync("test", (_, _) => { called = true; return default; }));
            Assert.True(called);
        }

        [Fact]
        public async Task A_call_that_throws_is_survived()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("throws", directory));

            var (succeeded, _) = await plugin.RunAsync<int>("test", (_, _) => throw new InvalidOperationException("boom"));

            Assert.False(succeeded);
            Assert.True(plugin.IsLoaded); //It is skipped for that call, not unloaded.
        }

        [Fact]
        public async Task A_call_that_takes_too_long_is_cut_off_and_told_to_stop()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("slow", directory, timeoutMilliseconds: 200));
            CancellationToken seen = default;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var succeeded = await plugin.RunAsync("test", async (_, token) => { seen = token; await Task.Delay(10_000, CancellationToken.None); });
            stopwatch.Stop();

            Assert.False(succeeded);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"{stopwatch.Elapsed}");
            Assert.True(seen.IsCancellationRequested);
        }

        [Fact]
        public async Task A_plugin_that_is_not_loaded_is_not_called()
        {
            using var directory = new TempDirectory();
            var plugin = await PluginSupport.Load(PluginSupport.Recorder("gone", directory));
            await plugin.StopAsync();

            Assert.False(await plugin.RunAsync("test", (_, _) => default));
        }
    }
}
