using Newtonsoft.Json.Linq;
using NetProxy.ClientApi;
using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using System.Net.Sockets;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// Changes made to the configuration files by hand, applied to the service that is running.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class ReloadTests(ManagementFixture service) : ManagementTest(service)
    {
        private static string FolderFile(string name) => Path.Combine(TestEnvironment.ConfigurationFolder, name);
        private static string ProxyFile => FolderFile("NpProxyConfiguration.json");
        private static string ServiceFile => FolderFile("NpConfiguration.json");

        private static JArray ReadProxies() => JArray.Parse(File.ReadAllText(ProxyFile));
        private static void WriteProxies(JArray proxies) => File.WriteAllText(ProxyFile, proxies.ToString());
        private static JObject Find(JArray proxies, string name) => proxies.OfType<JObject>().Single(o => (string?)o["Name"] == name);

        private static JObject Edited(string name, int listenPort, int endpointPort) => JObject.Parse($$"""
            {
              "Name": "{{name}}", "TrafficType": "Http", "ListenPort": {{listenPort}}, "AutoStart": true, "ListenOnAllAddresses": false,
              "Bindings": [ { "Enabled": true, "Address": "127.0.0.1" } ],
              "Endpoints": { "Collection": [ { "Enabled": true, "Address": "127.0.0.1", "Port": {{endpointPort}}, "TrafficType": "Http" } ] }
            }
            """);

        private void AssertNothingChanged(params string[] proxyNames)
        {
            Assert.Equal(proxyNames.Order(), Client.ListProxies().Select(o => o.Name).Order());
        }

        //---------------------------------------------------------------- Applying edits

        [Fact]
        public void Files_that_were_not_touched_change_nothing()
        {
            Client.ApplyProxy(NewProxy("web"));
            var running = Service.Manager.FindProxy(null, "web");

            var reply = Client.Reload();

            Assert.True(reply.Success);
            Assert.Empty(reply.Changes);
            Assert.Empty(reply.Problems);
            Assert.Same(running, Service.Manager.FindProxy(null, "web"));
        }

        [Fact]
        public async Task A_proxy_edited_in_the_file_is_replaced_and_the_others_keep_running_with_their_connections()
        {
            var edited = NewProxy("edited", "OLD");
            var untouched = NewProxy("untouched", "UNTOUCHED", c => { c.TrafficType = TrafficType.Raw; });
            var echo = Host.Own(new TcpEchoBackend());
            untouched.Endpoints.Collection[0].Port = echo.Port;
            Client.ApplyProxy(edited);
            Client.ApplyProxy(untouched);
            var untouchedProxy = Service.Manager.FindProxy(null, "untouched");

            using var connection = new TcpClient();
            await connection.ConnectAsync("127.0.0.1", untouched.ListenPort);
            await connection.GetStream().WriteAsync("before"u8.ToArray());
            await connection.GetStream().ReadAsync(new byte[6]);

            var proxies = ReadProxies();
            Find(proxies, "edited")["Endpoints"]!["Collection"]![0]!["Port"] = Host.Backend("NEW").Port;
            WriteProxies(proxies);
            var reply = Client.Reload();

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.Equal(["Proxy 'edited': changed."], reply.Changes);
            Assert.Contains("backend=NEW", await Get(edited));
            Assert.Same(untouchedProxy, Service.Manager.FindProxy(null, "untouched"));

            await connection.GetStream().WriteAsync("after!"u8.ToArray()); //The connection through the untouched proxy lived through the reload.
            var buffer = new byte[6];
            var read = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (read < 6)
            {
                read += await connection.GetStream().ReadAsync(buffer.AsMemory(read), timeout.Token);
            }
            Assert.Equal("after!", System.Text.Encoding.ASCII.GetString(buffer));
        }

        [Fact]
        public async Task A_proxy_written_by_hand_without_ids_is_added_and_started_and_gets_ids_when_saved()
        {
            var port = Net.FreePort();
            var proxies = ReadProxies();
            proxies.Add(Edited("by-hand", port, Host.Backend("HAND").Port));
            WriteProxies(proxies);

            var reply = Client.Reload();

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.Equal(["Proxy 'by-hand': added."], reply.Changes);
            Assert.Contains("backend=HAND", await Support.Client.Http(port));

            var saved = Find(ReadProxies(), "by-hand");
            Assert.NotEqual(Guid.Empty, Guid.Parse((string)saved["Id"]!));
            Assert.Empty(Client.Reload().Changes); //And now it is the same as what is in the file.
        }

        [Fact]
        public async Task A_proxy_removed_from_the_file_is_stopped_and_its_port_is_free()
        {
            var keep = NewProxy("keep");
            var remove = NewProxy("remove");
            Client.ApplyProxy(keep);
            Client.ApplyProxy(remove);

            var proxies = ReadProxies();
            proxies.Remove(Find(proxies, "remove"));
            WriteProxies(proxies);
            var reply = Client.Reload();

            Assert.Equal(["Proxy 'remove': removed."], reply.Changes);
            AssertNothingChanged("keep");
            await Assert.ThrowsAnyAsync<SocketException>(async () => { using var c = new TcpClient(); await c.ConnectAsync("127.0.0.1", remove.ListenPort); });
            Assert.Contains("backend=A", await Get(keep));
        }

        [Fact]
        public async Task A_proxy_whose_id_was_left_out_of_the_file_is_still_the_same_proxy()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var id = Client.GetProxy("web")!.Id;

            var proxies = ReadProxies();
            var entry = Find(proxies, "web");
            entry.Remove("Id");
            entry["Description"] = "edited by hand";
            WriteProxies(proxies);
            var reply = Client.Reload();

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.Single(Client.ListProxies());
            Assert.Equal(id, Client.GetProxy("web")!.Id);
            Assert.Equal("edited by hand", Client.GetProxy("web")!.Description);
            Assert.Contains("backend=A", await Get(proxy));
        }

        [Fact]
        public void A_proxy_that_was_stopped_and_is_changed_without_auto_start_stays_stopped()
        {
            var proxy = NewProxy("web", change: c => c.AutoStart = false);
            Client.ApplyProxy(proxy);

            var proxies = ReadProxies();
            Find(proxies, "web")["Description"] = "edited";
            WriteProxies(proxies);
            Client.Reload();

            Assert.False(Assert.Single(Client.ListProxies()).IsRunning);
        }

        [Fact]
        public async Task A_dry_run_says_what_would_change_and_changes_nothing()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var proxies = ReadProxies();
            Find(proxies, "web")["Description"] = "edited";
            proxies.Add(Edited("extra", Net.FreePort(), Host.Backend("X").Port));
            WriteProxies(proxies);

            var reply = Client.Reload(dryRun: true);

            Assert.True(reply.Success);
            Assert.Equal(2, reply.Changes.Count);
            AssertNothingChanged("web");
            Assert.Equal("", Client.GetProxy("web")!.Description);
            Assert.Contains("backend=A", await Get(proxy));
        }

        [Fact]
        public void A_plain_text_password_typed_into_the_file_is_encrypted_when_it_is_applied()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            var port = Net.FreePort();
            var proxies = ReadProxies();
            var entry = Edited("tls", port, Host.Backend("A").Port);
            entry["TrafficType"] = "Https";
            entry["TlsCertificatePath"] = pfx;
            entry["TlsCertificatePassword"] = Certs.Password;
            proxies.Add(entry);
            WriteProxies(proxies);

            var reply = Client.Reload();

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.True(Service.Manager.FindProxy(null, "tls")!.IsRunning); //The certificate loaded, so the plain text password was understood.
            var saved = (string)Find(ReadProxies(), "tls")["TlsCertificatePassword"]!;
            Assert.StartsWith("enc:", saved);
            Assert.DoesNotContain(Certs.Password, File.ReadAllText(ProxyFile));
            Assert.Equal(Certs.Password, Client.GetProxy("tls")!.TlsCertificatePassword); //The service still has the real one.
        }

        //---------------------------------------------------------------- Files that are wrong

        private void AssertRefused(string expectedProblem, params string[] expectedProxies)
        {
            var reply = Client.Reload();

            Assert.False(reply.Success);
            Assert.Contains(reply.Problems, o => o.Contains(expectedProblem, StringComparison.OrdinalIgnoreCase));
            Assert.Empty(reply.Changes);
            AssertNothingChanged(expectedProxies);
        }

        [Fact]
        public void A_file_that_is_not_json_is_refused_and_nothing_changes()
        {
            Client.ApplyProxy(NewProxy("web"));
            File.WriteAllText(ProxyFile, "[ this is { not json");

            var reply = Client.Reload();

            Assert.False(reply.Success);
            Assert.NotEmpty(reply.Problems);
            AssertNothingChanged("web");
        }

        [Fact]
        public void A_file_with_no_proxies_is_refused_because_that_is_what_a_broken_file_looks_like()
        {
            Client.ApplyProxy(NewProxy("web"));
            File.WriteAllText(ProxyFile, "[]");

            AssertRefused("probably a mistake", "web");
        }

        [Fact]
        public void Two_proxies_with_one_name_in_the_file_are_refused()
        {
            Client.ApplyProxy(NewProxy("web"));
            var proxies = ReadProxies();
            proxies.Add(Edited("web", Net.FreePort(), 9));
            proxies.Add(Edited("WEB", Net.FreePort(), 9));
            WriteProxies(proxies);

            AssertRefused("already a proxy named", "web");
        }

        [Fact]
        public void A_proxy_with_a_wrong_port_in_the_file_is_refused_and_so_is_everything_else_in_it()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var proxies = ReadProxies();
            Find(proxies, "web")["Description"] = "should not be applied";
            proxies.Add(Edited("bad", 70000, 9));
            WriteProxies(proxies);

            AssertRefused("listen port", "web");
            Assert.Equal("", Client.GetProxy("web")!.Description);
        }

        [Fact]
        public void Proxies_that_cannot_share_a_port_in_the_file_are_refused()
        {
            Client.ApplyProxy(NewProxy("web"));
            var port = Net.FreePort();
            var proxies = ReadProxies();
            proxies.Add(Edited("one", port, 9));
            proxies.Add(Edited("two", port, 9));
            WriteProxies(proxies);

            AssertRefused("already used", "web");
        }

        [Fact]
        public void A_proxy_that_cannot_be_started_after_the_reload_is_reported_but_the_rest_is_applied()
        {
            var taken = new TcpListener(System.Net.IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var port = ((System.Net.IPEndPoint)taken.LocalEndpoint).Port;
                var proxies = ReadProxies();
                proxies.Add(Edited("blocked", port, 9));
                WriteProxies(proxies);

                var reply = Client.Reload();

                Assert.True(reply.Success);
                Assert.Contains(reply.Problems, o => o.Contains("could not be started"));
                Assert.Contains("blocked", Client.ListProxies().Select(o => o.Name));
                Assert.False(Client.ListProxies().Single(o => o.Name == "blocked").IsRunning);
            }
            finally
            {
                taken.Stop();
            }
        }

        //---------------------------------------------------------------- Users, settings, the port

        [Fact]
        public void A_user_added_to_the_file_can_log_in_after_the_reload()
        {
            var service = JObject.Parse(File.ReadAllText(ServiceFile));
            ((JArray)service["Users"]!["Collection"]!).Add(JObject.FromObject(new
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "carol",
                PasswordHash = NetProxy.Library.Utilities.NpUtility.Sha256("carols-password"), //Typed in the clear, as a hash.
                Description = "by hand"
            }));
            File.WriteAllText(ServiceFile, service.ToString());

            var reply = Client.Reload();

            Assert.Contains("Users: changed.", reply.Changes);
            var options = Service.Options;
            options.UserName = "carol";
            options.PasswordHash = NpConnectionOptions.HashPassword("carols-password");
            using var carol = NpServiceClient.Connect(options);
            carol.GetInfo();
        }

        [Fact]
        public void A_file_without_users_is_refused_so_nobody_is_locked_out()
        {
            var service = JObject.Parse(File.ReadAllText(ServiceFile));
            ((JArray)service["Users"]!["Collection"]!).Clear();
            File.WriteAllText(ServiceFile, service.ToString());

            var reply = Client.Reload();

            Assert.False(reply.Success);
            Assert.Contains(reply.Problems, o => o.Contains("no users"));
            using var again = Service.Connect(); //Still possible.
        }

        [Fact]
        public void A_setting_changed_in_the_file_is_applied()
        {
            var service = JObject.Parse(File.ReadAllText(ServiceFile));
            service["Settings"]!["LogFileRetainedCount"] = 12;
            File.WriteAllText(ServiceFile, service.ToString());

            var reply = Client.Reload();

            Assert.Contains("Settings: changed.", reply.Changes);
            Assert.Equal(12, Client.GetSettings().LogFileRetainedCount);
        }

        [Fact]
        public void A_new_management_port_in_the_file_waits_for_the_restart_and_the_service_keeps_answering()
        {
            var service = JObject.Parse(File.ReadAllText(ServiceFile));
            var newPort = Net.FreePort();
            service["ManagementPort"] = newPort;
            File.WriteAllText(ServiceFile, service.ToString());
            try
            {
                var reply = Client.Reload();

                Assert.Contains(reply.Changes, o => o.Contains("management port", StringComparison.OrdinalIgnoreCase) && o.Contains("restarts"));
                using var stillThere = Service.Connect(); //On the port it has been listening on.
                stillThere.GetInfo();
            }
            finally
            {
                var restore = JObject.Parse(File.ReadAllText(ServiceFile));
                restore["ManagementPort"] = Service.Port;
                File.WriteAllText(ServiceFile, restore.ToString());
                Client.Reload();
            }
        }

        [Fact]
        public void Plugins_edited_in_the_plugin_file_are_applied_by_the_same_reload()
        {
            using var directory = new TempDirectory();
            Client.ApplyPlugin(PluginSupport.Recorder("rec", directory));
            var pluginFile = Directory.GetFiles(TestEnvironment.ConfigurationFolder, "*Plugin*").Single();

            var plugins = JArray.Parse(File.ReadAllText(pluginFile));
            plugins[0]["Description"] = "edited by hand";
            File.WriteAllText(pluginFile, plugins.ToString());
            var reply = Client.Reload();

            Assert.Contains("Plugin 'rec': changed.", reply.Changes);
            Assert.Equal("edited by hand", Client.ListPlugins().Single().Description);
        }
    }
}
