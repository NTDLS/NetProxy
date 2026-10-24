using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Plugins;
using NetProxy.Tests.Support;
using NTDLS.Persistence;
using Xunit;

namespace NetProxy.Tests.Plugins
{
    [Collection(ServiceCollection.Name)]
    public class PluginManagerTests : IDisposable
    {
        private readonly TempDirectory _directory = new();
        private readonly List<NpPluginManager> _managers = [];

        public PluginManagerTests() => TestEnvironment.ResetConfiguration();

        public void Dispose()
        {
            foreach (var manager in _managers)
            {
                manager.StopAsync().GetAwaiter().GetResult();
            }

            _directory.Dispose();
            TestEnvironment.ResetConfiguration();
        }

        private NpPluginManager Manager()
        {
            var manager = new NpPluginManager();
            _managers.Add(manager);
            return manager;
        }

        private NpPluginConfiguration Recorder(string name, Dictionary<string, string>? settings = null)
            => PluginSupport.Recorder(name, _directory, settings);

        private static List<string> Names(NpPluginManager manager) => manager.GetGridItems().Select(o => o.Name).ToList();

        //---------------------------------------------------------------- Adding and changing

        [Fact]
        public async Task A_plugin_is_added_at_the_end_and_loaded()
        {
            var manager = Manager();

            Assert.Null(await manager.UpsertAsync(Recorder("one")));
            Assert.Null(await manager.UpsertAsync(Recorder("two")));

            Assert.Equal(["one", "two"], Names(manager));
            Assert.All(manager.GetGridItems(), o => Assert.True(o.IsLoaded));
            Assert.Equal("Recorder", manager.GetGridItems()[0].PluginName);
        }

        [Fact]
        public async Task Saving_a_plugin_again_replaces_it_where_it_is_and_loads_it_again()
        {
            var manager = Manager();
            var one = Recorder("one");
            await manager.UpsertAsync(one);
            await manager.UpsertAsync(Recorder("two"));

            var changed = Recorder("one", new() { ["Tag"] = "changed" });
            changed.Id = one.Id;
            changed.Description = "now with a description";
            Assert.Null(await manager.UpsertAsync(changed));

            Assert.Equal(["one", "two"], Names(manager));
            Assert.Equal("now with a description", manager.GetGridItems()[0].Description);
            Assert.Contains(PluginSupport.Events(_directory, "one"), o => o.StartsWith("changed:ServiceStarting"));
            Assert.Contains("ServiceStopping", PluginSupport.Events(_directory, "one")); //The old one was told.
        }

        [Fact]
        public async Task Names_are_unique_whatever_the_case()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));

            var error = await manager.UpsertAsync(Recorder("ONE"));

            Assert.Contains("already a plugin named", error);
            Assert.Single(manager.GetGridItems());
        }

        [Theory]
        [InlineData("", "x.dll", "needs a name")]
        [InlineData("   ", "x.dll", "needs a name")]
        [InlineData("n", "", "needs an assembly")]
        public async Task A_plugin_needs_a_name_and_an_assembly(string name, string assembly, string reason)
        {
            var error = await Manager().UpsertAsync(new NpPluginConfiguration { Name = name, AssemblyPath = assembly });
            Assert.Contains(reason, error);
        }

        [Fact]
        public async Task Names_and_paths_are_trimmed_and_the_time_limit_is_kept_in_range()
        {
            var manager = Manager();
            var configuration = Recorder("  padded  ");
            configuration.AssemblyPath = "  " + configuration.AssemblyPath + "  ";
            configuration.TimeoutMilliseconds = 1;

            await manager.UpsertAsync(configuration);

            var item = manager.GetGridItems().Single();
            Assert.Equal("padded", item.Name);
            Assert.True(item.IsLoaded);
            Assert.Equal(10, item.TimeoutMilliseconds);
        }

        [Fact]
        public async Task A_plugin_that_cannot_be_loaded_is_still_saved_with_the_reason()
        {
            var manager = Manager();
            var configuration = Recorder("broken");
            configuration.AssemblyPath = _directory.File("nope.dll");

            Assert.Null(await manager.UpsertAsync(configuration));

            var item = manager.GetGridItems().Single();
            Assert.False(item.IsLoaded);
            Assert.Contains("nope.dll", item.LoadError);
        }

        [Fact]
        public async Task A_disabled_plugin_is_saved_but_not_loaded()
        {
            var manager = Manager();
            var configuration = Recorder("off");
            configuration.Enabled = false;

            await manager.UpsertAsync(configuration);

            var item = manager.GetGridItems().Single();
            Assert.False(item.IsLoaded);
            Assert.Equal("", item.LoadError);
            Assert.Empty(PluginSupport.Events(_directory, "off"));
        }

        [Fact]
        public async Task Reloading_picks_up_the_file_again()
        {
            var manager = Manager();
            var configuration = Recorder("one");
            await manager.UpsertAsync(configuration);

            Assert.True(await manager.ReloadAsync(configuration.Id));
            Assert.False(await manager.ReloadAsync(Guid.NewGuid()));
            Assert.Equal(2, PluginSupport.Events(_directory, "one").Count(o => o.StartsWith("ServiceStarting")));
        }

        //---------------------------------------------------------------- Order and removal

        [Fact]
        public async Task Plugins_are_moved_up_and_down_and_the_ends_stay_put()
        {
            var manager = Manager();
            var configurations = new[] { Recorder("a"), Recorder("b"), Recorder("c") };
            foreach (var configuration in configurations)
            {
                await manager.UpsertAsync(configuration);
            }

            await manager.MoveAsync(configurations[2].Id, -1);
            Assert.Equal(["a", "c", "b"], Names(manager));

            await manager.MoveAsync(configurations[0].Id, -1); //Already first.
            await manager.MoveAsync(configurations[1].Id, 1);  //Already last.
            Assert.Equal(["a", "c", "b"], Names(manager));

            await manager.MoveAsync(configurations[0].Id, 5);  //Only one step, in the direction.
            Assert.Equal(["c", "a", "b"], Names(manager));

            await manager.MoveAsync(Guid.NewGuid(), 1);
            Assert.Equal(["c", "a", "b"], Names(manager));
        }

        [Fact]
        public async Task A_deleted_plugin_is_told_and_gone()
        {
            var manager = Manager();
            var one = Recorder("one");
            await manager.UpsertAsync(one);
            await manager.UpsertAsync(Recorder("two"));

            Assert.True(await manager.DeleteAsync(one.Id));
            Assert.False(await manager.DeleteAsync(one.Id));

            Assert.Equal(["two"], Names(manager));
            Assert.Contains("ServiceStopping", PluginSupport.Events(_directory, "one"));
        }

        //---------------------------------------------------------------- Who gets called

        [Fact]
        public async Task A_proxy_gets_only_the_plugins_it_chose_in_the_order_of_the_service()
        {
            var manager = Manager();
            var a = Recorder("a");
            var b = Recorder("b");
            var c = Recorder("c");
            foreach (var configuration in new[] { a, b, c })
            {
                await manager.UpsertAsync(configuration);
            }

            var proxy = new NpProxyConfiguration { PluginIds = [c.Id, a.Id] };

            Assert.Equal(["a", "c"], manager.ForProxy(proxy).Plugins.Select(o => o.Configuration.Name));
            Assert.True(manager.ForProxy(new NpProxyConfiguration()).IsEmpty);
        }

        [Fact]
        public async Task A_proxy_does_not_get_plugins_that_are_disabled_or_not_loaded()
        {
            var manager = Manager();
            var ok = Recorder("ok");
            var off = Recorder("off");
            off.Enabled = false;
            var broken = Recorder("broken");
            broken.AssemblyPath = _directory.File("nope.dll");
            foreach (var configuration in new[] { ok, off, broken })
            {
                await manager.UpsertAsync(configuration);
            }

            var proxy = new NpProxyConfiguration { PluginIds = [ok.Id, off.Id, broken.Id] };

            Assert.Equal(["ok"], manager.ForProxy(proxy).Plugins.Select(o => o.Configuration.Name));
            Assert.Equal(["ok"], manager.ForService().Plugins.Select(o => o.Configuration.Name));
        }

        //---------------------------------------------------------------- Keeping them

        [Fact]
        public async Task The_plugins_come_back_in_their_order_when_the_service_starts_again()
        {
            var first = Manager();
            var a = Recorder("a", new() { ["Tag"] = "x" });
            var b = Recorder("b");
            await first.UpsertAsync(a);
            await first.UpsertAsync(b);
            await first.MoveAsync(b.Id, -1);
            await first.StopAsync();

            var second = Manager();
            await second.StartAsync();

            Assert.Equal(["b", "a"], Names(second));
            var items = second.GetGridItems();
            Assert.Equal(a.Id, items[1].Id);
            Assert.Equal("x", items[1].Settings["Tag"]);
            Assert.All(items, o => Assert.True(o.IsLoaded));
        }

        [Fact]
        public async Task Starting_tells_the_plugins_and_stopping_tells_them_too()
        {
            var first = Manager();
            await first.UpsertAsync(Recorder("one"));
            await first.StopAsync();
            File.Delete(PluginSupport.LogOf(_directory, "one"));

            var second = Manager();
            await second.StartAsync();
            Assert.Contains(PluginSupport.Events(_directory, "one"), o => o.StartsWith("ServiceStarting"));

            await second.StopAsync();
            Assert.Contains("ServiceStopping", PluginSupport.Events(_directory, "one"));
        }

        [Fact]
        public async Task A_plugin_with_a_tick_interval_gets_ticks()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("ticker", new() { ["TickSeconds"] = "1" }));
            await manager.StopAsync();

            var running = Manager();
            await running.StartAsync();

            var ticks = await Client.Eventually(() => Task.FromResult(PluginSupport.Events(_directory, "ticker").Count(o => o == "Tick")), o => o >= 2, seconds: 10);
            Assert.True(ticks >= 2, $"{ticks} ticks");
        }

        //---------------------------------------------------------------- Changes made to the file by hand

        private void WriteFile(List<NpPluginConfiguration> plugins)
            => CommonApplicationData.SaveToDisk(TestEnvironment.ConfigurationName, plugins);

        private static List<NpPluginConfiguration> Copy(NpPluginManager manager)
            => manager.GetGridItems().Select(o => new NpPluginConfiguration
            {
                Id = o.Id,
                Name = o.Name,
                Description = o.Description,
                Enabled = o.Enabled,
                AssemblyPath = o.AssemblyPath,
                TypeName = o.TypeName,
                Settings = new Dictionary<string, string>(o.Settings),
                TimeoutMilliseconds = o.TimeoutMilliseconds
            }).ToList();

        [Fact]
        public async Task A_file_that_is_the_same_changes_nothing()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.Empty(result.Problems);
            Assert.Empty(result.Changes);
        }

        [Fact]
        public async Task A_change_in_the_file_reloads_only_that_plugin()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            await manager.UpsertAsync(Recorder("two"));
            var edited = Copy(manager);
            edited[0].Settings["Tag"] = "edited";
            WriteFile(edited);

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.Equal(["Plugin 'one': changed."], result.Changes);
            Assert.Contains(PluginSupport.Events(_directory, "one"), o => o.StartsWith("edited:ServiceStarting"));
            Assert.Single(PluginSupport.Events(_directory, "two"), o => o.StartsWith("ServiceStarting")); //Not touched.
        }

        [Fact]
        public async Task Plugins_added_removed_and_reordered_in_the_file_are_applied()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            await manager.UpsertAsync(Recorder("two"));
            var edited = Copy(manager);
            edited.RemoveAt(0);
            edited.Add(Recorder("three"));
            edited.Reverse();
            WriteFile(edited);

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.Empty(result.Problems);
            Assert.Equal(["three", "two"], Names(manager));
            Assert.Contains(result.Changes, o => o.Contains("'one'") && o.Contains("removed"));
            Assert.Contains(result.Changes, o => o.Contains("'three'") && o.Contains("added"));
            Assert.All(manager.GetGridItems(), o => Assert.True(o.IsLoaded));
        }

        [Fact]
        public async Task A_dry_run_says_what_would_change_and_changes_nothing()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            var edited = Copy(manager);
            edited[0].Description = "edited";
            edited.Add(Recorder("two"));
            WriteFile(edited);

            var result = await manager.ReloadFromDiskAsync(dryRun: true);

            Assert.Equal(2, result.Changes.Count);
            Assert.Equal(["one"], Names(manager));
            Assert.Equal("", manager.GetGridItems()[0].Description);
        }

        [Fact]
        public async Task A_plugin_whose_id_was_left_out_of_the_file_is_still_the_same_plugin()
        {
            var manager = Manager();
            var one = Recorder("one");
            await manager.UpsertAsync(one);
            var edited = Copy(manager);
            edited[0].Id = Guid.NewGuid();
            edited[0].Description = "edited";
            WriteFile(edited);

            await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.Equal(one.Id, manager.GetGridItems().Single().Id);
            Assert.Equal("edited", manager.GetGridItems().Single().Description);
        }

        [Fact]
        public async Task A_file_with_no_plugins_is_refused_because_it_is_what_a_broken_file_looks_like()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            WriteFile([]);

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.Contains("probably a mistake", Assert.Single(result.Problems));
            Assert.Equal(["one"], Names(manager));
        }

        [Fact]
        public async Task A_file_with_two_plugins_of_one_name_or_without_an_assembly_is_refused_whole()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            var edited = Copy(manager);
            edited[0].Description = "should not be applied";
            edited.Add(Recorder("ONE"));
            WriteFile(edited);

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.NotEmpty(result.Problems);
            Assert.Equal("", manager.GetGridItems().Single().Description);

            edited.RemoveAt(1);
            edited[0].AssemblyPath = "";
            WriteFile(edited);
            Assert.NotEmpty((await manager.ReloadFromDiskAsync(dryRun: false)).Problems);
        }

        [Fact]
        public async Task A_file_that_is_not_json_is_refused()
        {
            var manager = Manager();
            await manager.UpsertAsync(Recorder("one"));
            var path = Directory.GetFiles(TestEnvironment.ConfigurationFolder, "*Plugin*").Single();
            await File.WriteAllTextAsync(path, "{ this is not json");

            var result = await manager.ReloadFromDiskAsync(dryRun: false);

            Assert.NotEmpty(result.Problems);
            Assert.Equal(["one"], Names(manager));
        }
    }
}
