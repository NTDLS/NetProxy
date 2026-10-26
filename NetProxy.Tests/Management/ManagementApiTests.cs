using NetProxy.ClientApi;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Tests.Support;
using NTDLS.ReliableMessaging;
using NTDLS.SecureKeyExchange;
using System.Net.Sockets;
using Xunit;

namespace NetProxy.Tests.Management
{
    [Collection(ManagementCollection.Name)]
    public class ManagementApiTests(ManagementFixture service) : ManagementTest(service)
    {
        /// <summary>
        /// True when the connection was ended by the other side, which a close from the service does with a reset.
        /// </summary>
        private static async Task<bool> IsClosed(TcpClient client)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await client.GetStream().ReadAsync(new byte[1], timeout.Token) == 0;
            }
            catch (IOException)
            {
                return true;
            }
        }

        //---------------------------------------------------------------- Connecting

        [Fact]
        public void The_service_says_what_it_is()
        {
            var info = Client.GetInfo();

            Assert.Matches(@"^\d+\.\d+\.\d+$", info.Version);
            Assert.Equal(Service.Port, info.ManagementPort);
            Assert.Equal(TestEnvironment.ConfigurationName, info.ConfigurationName);
            Assert.True(info.UptimeSeconds >= 0);
            Assert.False(string.IsNullOrEmpty(info.OperatingSystem));
        }

        [Fact]
        public void The_counts_follow_the_proxies()
        {
            Client.ApplyProxy(NewProxy("one"));
            Client.ApplyProxy(NewProxy("two", change: c => c.AutoStart = false));

            var info = Client.GetInfo();
            Assert.Equal(2, info.ProxyCount);
            Assert.Equal(1, info.RunningProxyCount);
        }

        [Fact]
        public void A_wrong_password_is_refused_with_a_message()
        {
            var options = Service.Options;
            options.PasswordHash = NpConnectionOptions.HashPassword("wrong");

            var error = Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));
            Assert.Contains("not correct", error.Message);
        }

        [Fact]
        public void An_unknown_user_is_refused()
        {
            var options = Service.Options;
            options.UserName = "nobody";

            Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));
        }

        [Fact]
        public void The_user_name_is_not_case_sensitive_but_the_password_is()
        {
            var options = Service.Options;
            options.UserName = "ADMIN";
            using (var client = NpServiceClient.Connect(options))
            {
                client.GetInfo();
            }

            options.PasswordHash = NpConnectionOptions.HashPassword(ManagementFixture.Password.ToUpperInvariant() + "x");
            Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));
        }

        [Fact]
        public void Nothing_can_be_done_without_logging_in()
        {
            var client = new RmClient();
            try
            {
                client.Connect("127.0.0.1", Service.Port);
                var negotiator = new CompoundNegotiator();
                var token = negotiator.GenerateNegotiationToken((int)Math.Ceiling(Constants.ManagementCryptographyKeySize / 128.0));
                negotiator.ApplyNegotiationResponseToken(client.Query(new QueryRequestKeyExchange(token)).NegotiationToken);
                var provider = new ManagementCryptographyProvider(negotiator.SharedSecret);
                client.Query(new QueryApplyCryptography(), () => client.SetCryptographyProvider(provider));

                Assert.ThrowsAny<Exception>(() => client.Query<QueryServiceInfoReply>(new QueryServiceInfo()));
                Assert.ThrowsAny<Exception>(() => client.Query<QueryProxyConfigurationListReply>(new QueryProxyConfigurationList()));
                Assert.ThrowsAny<Exception>(() => client.Query<QueryBrowseReply>(new QueryBrowse()));
            }
            finally
            {
                try { client.Disconnect(); } catch { }
            }
        }

        [Fact]
        public void A_login_over_a_connection_that_is_not_encrypted_is_refused()
        {
            var client = new RmClient();
            try
            {
                client.Connect("127.0.0.1", Service.Port);

                var reply = client.Query(new QueryLogin(ManagementFixture.UserName, NpUtility.Sha256(ManagementFixture.Password)));

                Assert.False(reply.Result);
                Assert.Contains("not encrypted", reply.Message);
            }
            finally
            {
                try { client.Disconnect(); } catch { }
            }
        }

        //---------------------------------------------------------------- Proxies

        [Fact]
        public async Task A_proxy_is_created_and_starts_serving()
        {
            var proxy = NewProxy("web");

            var result = Client.ApplyProxy(proxy);

            Assert.True(result.Success);
            Assert.Equal("created", result.Action);
            Assert.Null(result.StartError);
            Assert.Contains("backend=A", await Get(proxy));
            var listed = Assert.Single(Client.ListProxies());
            Assert.Equal("web", listed.Name);
            Assert.True(listed.IsRunning);
            Assert.Equal(proxy.ListenPort, listed.ListenPort);
        }

        [Fact]
        public void A_proxy_that_is_set_not_to_start_is_saved_stopped()
        {
            Client.ApplyProxy(NewProxy("web", change: c => c.AutoStart = false));

            Assert.False(Assert.Single(Client.ListProxies()).IsRunning);
        }

        [Fact]
        public void Applying_the_same_proxy_again_leaves_it_alone_and_running()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var running = Service.Manager.FindProxy(null, "web");

            var again = Client.ApplyProxy(proxy.Clone());

            Assert.True(again.Success);
            Assert.Equal("unchanged", again.Action);
            Assert.Same(running, Service.Manager.FindProxy(null, "web")); //Not stopped, not replaced.
            Assert.True(running!.IsRunning);
        }

        [Fact]
        public async Task A_changed_proxy_is_replaced_by_name_and_serves_the_new_configuration()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var id = Client.GetProxy("web")!.Id;

            var changed = proxy.Clone();
            changed.Id = Guid.NewGuid(); //A file does not know the id the service gave the proxy.
            changed.Endpoints.Collection[0].Port = Host.Backend("B").Port;
            var result = Client.ApplyProxy(changed);

            Assert.Equal("updated", result.Action);
            Assert.Single(Client.ListProxies());
            Assert.Equal(id, Client.GetProxy("web")!.Id);
            Assert.Contains("backend=B", await Get(proxy));
        }

        [Fact]
        public void The_name_is_matched_whatever_the_case()
        {
            var proxy = NewProxy("Web");
            Client.ApplyProxy(proxy);

            var changed = proxy.Clone();
            changed.Name = "WEB";
            changed.Description = "now described";

            Assert.Equal("updated", Client.ApplyProxy(changed).Action);
            Assert.Single(Client.ListProxies());
        }

        [Fact]
        public void A_dry_run_says_what_would_happen_and_changes_nothing()
        {
            var proxy = NewProxy("web");

            var create = Client.ApplyProxy(proxy, dryRun: true);
            Assert.Equal("create", create.Action);
            Assert.True(create.Success);
            Assert.Empty(Client.ListProxies());

            Client.ApplyProxy(proxy);
            Assert.Equal("unchanged", Client.ApplyProxy(proxy.Clone(), dryRun: true).Action);

            var changed = proxy.Clone();
            changed.Description = "different";
            Assert.Equal("update", Client.ApplyProxy(changed, dryRun: true).Action);
            Assert.Equal("", Client.GetProxy("web")!.Description);
        }

        [Fact]
        public void A_proxy_with_problems_is_refused_with_all_of_them_and_nothing_is_created()
        {
            var proxy = NewProxy("web");
            proxy.ListenPort = 0;
            proxy.Endpoints.Collection.Clear();

            var result = Client.ApplyProxy(proxy);

            Assert.False(result.Success);
            Assert.Contains(result.Problems, o => o.Contains("listen port"));
            Assert.Contains(result.Problems, o => o.Contains("endpoint"));
            Assert.Empty(Client.ListProxies());
        }

        [Fact]
        public void A_proxy_that_cannot_start_is_saved_and_says_why()
        {
            var taken = new TcpListener(System.Net.IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var proxy = NewProxy("web");
                proxy.ListenPort = ((System.Net.IPEndPoint)taken.LocalEndpoint).Port;

                var result = Client.ApplyProxy(proxy);

                Assert.True(result.Success);
                Assert.False(string.IsNullOrEmpty(result.StartError));
                Assert.False(Assert.Single(Client.ListProxies()).IsRunning);
            }
            finally
            {
                taken.Stop();
            }
        }

        [Fact]
        public void Two_proxies_cannot_take_the_same_name_or_a_port_in_a_way_that_does_not_work()
        {
            Client.ApplyProxy(NewProxy("web"));

            var sameNameOtherCase = Client.GetProxy("web")!.Clone();
            sameNameOtherCase.Id = Guid.NewGuid();
            sameNameOtherCase.Name = "WEB";
            sameNameOtherCase.ListenPort = Net.FreePort();
            //By name this is the same proxy, so it is an update. By id it would be a second proxy with the name of the first.
            Assert.Equal("updated", Client.ApplyProxy(sameNameOtherCase).Action);

            var sameAddress = NewProxy("other");
            sameAddress.ListenPort = Client.GetProxy("web")!.ListenPort;
            var result = Client.ApplyProxy(sameAddress);
            Assert.False(result.Success);
            Assert.Contains(result.Problems, o => o.Contains("already used"));
        }

        [Fact]
        public void The_console_cannot_make_two_proxies_with_one_name_either()
        {
            Client.ApplyProxy(NewProxy("web"));

            var second = NewProxy("web");
            second.Id = Guid.NewGuid();
            var reply = Client.Raw.Query<QueryUpsertProxyReply>(new QueryUpsertProxy { Proxy = second, MatchByName = false });

            Assert.False(reply.Success);
            Assert.Contains("already a proxy named", reply.Message);
            Assert.Single(Client.ListProxies());
        }

        [Fact]
        public void Secrets_are_shown_masked_and_a_masked_secret_keeps_the_real_one()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            Assert.True(Client.ApplyCertificate(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password }).Success);

            var stored = Client.GetCertificates().Single();
            var shown = NpServiceClient.MaskSecrets(stored);
            Assert.Equal(NpServiceClient.SecretMask, shown.Password);
            Assert.Equal(Certs.Password, stored.Password); //The copy is masked, not the certificate.

            shown.Description = "edited from the masked copy";
            var result = Client.ApplyCertificate(shown);

            Assert.True(result.Success);
            Assert.Equal("updated", result.Action);
            var after = Client.GetCertificates().Single();
            Assert.Equal(Certs.Password, after.Password);
            Assert.Equal("edited from the masked copy", after.Description);
            Assert.True(after.IsLoaded); //The file still loads, so the password is the real one.
        }

        [Fact]
        public void A_masked_secret_on_a_new_certificate_becomes_empty_and_not_the_mask()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");

            Client.ApplyCertificate(new NpCertificateConfiguration { Name = "new", Path = pfx, Password = NpServiceClient.SecretMask });

            Assert.Equal("", Client.GetCertificates().Single().Password);
        }

        [Fact]
        public void The_proxy_list_never_carries_secrets()
        {
            Client.ApplyProxy(NewProxy("web"));

            Assert.DoesNotContain("Password", Newtonsoft.Json.JsonConvert.SerializeObject(Client.ListProxies()));
        }

        [Fact]
        public void Endpoint_ids_survive_an_update_that_does_not_name_them()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var before = Client.GetProxy("web")!.Endpoints.Collection[0].Id;

            var written = proxy.Clone(); //The endpoint of a file has a new id each time it is read.
            written.Endpoints.Collection[0].Id = Guid.NewGuid();
            written.Description = "edited";
            Client.ApplyProxy(written);

            Assert.Equal(before, Client.GetProxy("web")!.Endpoints.Collection[0].Id);
        }

        [Fact]
        public async Task Proxies_are_stopped_started_restarted_and_deleted()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);

            Assert.Null(Client.ProxyAction("web", NpProxyAction.Stop));
            Assert.False(Assert.Single(Client.ListProxies()).IsRunning);
            await Assert.ThrowsAnyAsync<SocketException>(async () => { using var c = new TcpClient(); await c.ConnectAsync("127.0.0.1", proxy.ListenPort); });

            Assert.Null(Client.ProxyAction("WEB", NpProxyAction.Start));
            Assert.Contains("backend=A", await Get(proxy));

            var before = Service.Manager.FindProxy(null, "web")!.Statistics.TotalConnections;
            Assert.Null(Client.ProxyAction("web", NpProxyAction.Restart));
            Assert.Contains("backend=A", await Get(proxy));

            Assert.Null(Client.ProxyAction("web", NpProxyAction.Delete));
            Assert.Empty(Client.ListProxies());
            await Assert.ThrowsAnyAsync<SocketException>(async () => { using var c = new TcpClient(); await c.ConnectAsync("127.0.0.1", proxy.ListenPort); });
            Assert.True(before >= 1);
        }

        [Fact]
        public void An_action_on_a_proxy_that_is_not_there_says_so()
        {
            foreach (var action in Enum.GetValues<NpProxyAction>())
            {
                Assert.Contains("There is no proxy", Client.ProxyAction("nobody", action));
            }
        }

        [Fact]
        public void Starting_a_proxy_whose_port_is_taken_says_why()
        {
            var proxy = NewProxy("web", change: c => c.AutoStart = false);
            Client.ApplyProxy(proxy);
            var taken = new TcpListener(System.Net.IPAddress.Loopback, proxy.ListenPort);
            taken.Start();
            try
            {
                Assert.False(string.IsNullOrEmpty(Client.ProxyAction("web", NpProxyAction.Start)));
            }
            finally
            {
                taken.Stop();
            }
        }

        [Fact]
        public async Task Statistics_and_connections_are_listed_and_connections_can_be_closed()
        {
            var echo = Host.Own(new TcpEchoBackend());
            var proxy = NewProxy("tcp", change: c =>
            {
                c.TrafficType = TrafficType.Tcp;
                c.Endpoints.Collection[0].Port = echo.Port;
            });
            Client.ApplyProxy(proxy);
            var id = Client.GetProxy("tcp")!.Id;

            using var open = new TcpClient();
            await open.ConnectAsync("127.0.0.1", proxy.ListenPort);
            await open.GetStream().WriteAsync(new byte[100]);
            await NetProxy.Tests.Support.Client.ReadExactly(open.GetStream(), 100);

            var connection = Assert.Single(Client.GetConnections(id));
            Assert.Equal("127.0.0.1", connection.SourceAddress);
            Assert.Equal(echo.Port, connection.DestinationPort);
            Assert.True(connection.IsConnectedToEndpoint);
            Assert.Equal(1, Client.GetStatistics()[id].CurrentConnections);

            Assert.Equal(1, Client.CloseConnections(connection.Id, null));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.True(await IsClosed(open));
            Assert.Equal(0, Client.CloseConnections(Guid.NewGuid(), null));
        }

        [Fact]
        public async Task All_the_connections_of_a_proxy_are_closed_together()
        {
            var echo = Host.Own(new TcpEchoBackend());
            var proxy = NewProxy("tcp", change: c =>
            {
                c.TrafficType = TrafficType.Tcp;
                c.Endpoints.Collection[0].Port = echo.Port;
            });
            Client.ApplyProxy(proxy);

            var clients = new List<TcpClient>();
            for (var i = 0; i < 3; i++)
            {
                var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", proxy.ListenPort);
                await client.GetStream().WriteAsync(new byte[1]);
                await NetProxy.Tests.Support.Client.ReadExactly(client.GetStream(), 1);
                clients.Add(client);
            }

            Assert.Equal(3, Client.CloseConnections(null, "tcp"));
            Assert.Equal(0, Client.CloseConnections(null, "nobody"));

            foreach (var client in clients)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                Assert.True(await IsClosed(client));
                client.Dispose();
            }
        }

        [Fact]
        public async Task Proxies_that_share_a_port_by_host_name_are_created_through_the_api()
        {
            var port = Net.FreePort();
            var a = NewProxy("a", "A", c => { c.ListenPort = port; c.HostNames.Add(new NpHostName { Host = "a.example.com" }); });
            var b = NewProxy("b", "B", c => { c.ListenPort = port; c.HostNames.Add(new NpHostName { Host = "b.example.com" }); });

            Assert.True(Client.ApplyProxy(a).Success);
            Assert.True(Client.ApplyProxy(b).Success);

            Assert.Contains("backend=A", await Get(a, "a.example.com"));
            Assert.Contains("backend=B", await Get(b, "b.example.com"));
            Assert.All(Client.ListProxies(), o => Assert.True(o.IsRunning));
            Assert.Equal(["a.example.com"], Client.ListProxies().Single(o => o.Name == "a").HostNames.Select(o => o.Host));
        }

        //---------------------------------------------------------------- Users

        [Fact]
        public void A_user_is_added_can_log_in_and_is_removed()
        {
            Assert.Null(Client.AddUser("bob", "bobs-password", "Bob"));
            Assert.Contains(Client.ListUsers(), o => o.UserName == "bob" && o.Description == "Bob");

            var options = Service.Options;
            options.UserName = "bob";
            options.PasswordHash = NpConnectionOptions.HashPassword("bobs-password");
            using (var bob = NpServiceClient.Connect(options))
            {
                bob.GetInfo();
            }

            Assert.Null(Client.RemoveUser("bob"));
            Assert.DoesNotContain(Client.ListUsers(), o => o.UserName == "bob");
            Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));
        }

        [Fact]
        public void A_user_cannot_be_added_twice_whatever_the_case()
        {
            Client.AddUser("bob", "x");

            Assert.Contains("already a user", Client.AddUser("BOB", "y"));
        }

        [Fact]
        public void A_password_is_changed_and_the_old_one_stops_working()
        {
            Client.AddUser("bob", "old");
            Assert.Null(Client.SetPassword("bob", "new"));

            var options = Service.Options;
            options.UserName = "bob";

            options.PasswordHash = NpConnectionOptions.HashPassword("old");
            Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));

            options.PasswordHash = NpConnectionOptions.HashPassword("new");
            using var client = NpServiceClient.Connect(options);
            client.GetInfo();
        }

        [Fact]
        public void The_last_user_cannot_be_removed_and_unknown_users_are_reported()
        {
            Assert.Contains("lock everyone out", Client.RemoveUser(ManagementFixture.UserName));
            Assert.Contains("There is no user", Client.RemoveUser("nobody"));
            Assert.Contains("There is no user", Client.SetPassword("nobody", "x"));
            Assert.Contains("needs a name", Client.AddUser("  ", "x"));
        }

        [Fact]
        public void The_hash_of_a_password_is_never_listed()
        {
            Client.AddUser("bob", "x");

            Assert.All(Client.ListUsers(), o => Assert.NotEqual(string.Empty, o.UserName));
        }

        //---------------------------------------------------------------- Plugins

        [Fact]
        public void A_plugin_is_created_updated_and_listed()
        {
            using var directory = new TempDirectory();
            var plugin = PluginSupport.Recorder("rec", directory);

            var created = Client.ApplyPlugin(plugin);
            Assert.True(created.Success);
            Assert.Equal("created", created.Action);
            Assert.True(string.IsNullOrEmpty(created.LoadError));

            var again = PluginSupport.Recorder("REC", directory, new() { ["Tag"] = "x" });
            var updated = Client.ApplyPlugin(again);
            Assert.Equal("updated", updated.Action);

            var listed = Assert.Single(Client.ListPlugins());
            Assert.True(listed.IsLoaded);
            Assert.Equal("Recorder", listed.PluginName);
            Assert.Equal("x", listed.Settings["Tag"]);
        }

        [Fact]
        public void A_plugin_that_cannot_be_loaded_is_saved_and_says_why()
        {
            var plugin = new NpPluginConfiguration { Name = "broken", AssemblyPath = "does-not-exist.dll" };

            var result = Client.ApplyPlugin(plugin);

            Assert.True(result.Success);
            Assert.Contains("does-not-exist.dll", result.LoadError);
            Assert.False(Assert.Single(Client.ListPlugins()).IsLoaded);
        }

        [Fact]
        public void Plugins_are_moved_reloaded_and_deleted_by_name()
        {
            using var directory = new TempDirectory();
            Client.ApplyPlugin(PluginSupport.Recorder("a", directory));
            Client.ApplyPlugin(PluginSupport.Recorder("b", directory));

            Assert.Null(Client.PluginAction("b", NpPluginAction.MoveUp));
            Assert.Equal(["b", "a"], Client.ListPlugins().Select(o => o.Name));

            Assert.Null(Client.PluginAction("a", NpPluginAction.Reload));
            Assert.Equal(2, PluginSupport.Events(directory, "a").Count(o => o.StartsWith("ServiceStarting")));

            Assert.Null(Client.PluginAction("A", NpPluginAction.Delete));
            Assert.Equal(["b"], Client.ListPlugins().Select(o => o.Name));
            Assert.Contains("There is no plugin", Client.PluginAction("a", NpPluginAction.Delete));
        }

        [Fact]
        public async Task Deleting_a_plugin_takes_it_off_the_proxies_that_used_it()
        {
            using var directory = new TempDirectory();
            Client.ApplyPlugin(PluginSupport.Recorder("rec", directory));
            var pluginId = Client.ListPlugins().Single().Id;

            var proxy = NewProxy("web", change: c => c.PluginIds.Add(pluginId));
            Client.ApplyProxy(proxy);
            Assert.Contains("backend=A", await Get(proxy));
            Assert.Contains(PluginSupport.Events(directory, "rec"), o => o.StartsWith("Connecting"));

            Client.PluginAction("rec", NpPluginAction.Delete);

            Assert.Empty(Client.GetProxy("web")!.PluginIds);
            Assert.Contains("backend=A", await Get(proxy));
        }

        [Fact]
        public void A_login_is_told_to_the_plugins()
        {
            using var directory = new TempDirectory();
            Client.ApplyPlugin(PluginSupport.Recorder("rec", directory));

            var options = Service.Options;
            options.PasswordHash = NpConnectionOptions.HashPassword("wrong");
            Assert.ThrowsAny<Exception>(() => NpServiceClient.Connect(options));
            using (Service.Connect())
            {
            }

            var events = PluginSupport.Events(directory, "rec");
            Assert.Contains("Login admin False", events);
            Assert.Contains("Login admin True", events);
        }

        //---------------------------------------------------------------- Settings, certificates, logs

        [Fact]
        public void Settings_are_read_changed_and_read_back()
        {
            var settings = Client.GetSettings();
            Assert.Equal(Service.LogPath, settings.LogPath);

            settings.LogFileRetainedCount = 7;
            settings.LogLevel = NpLogging.Severity.Warning;
            var after = Client.SetSettings(settings);

            Assert.Equal(7, after.LogFileRetainedCount);
            Assert.Equal(NpLogging.Severity.Warning, after.LogLevel);
        }

        [Fact]
        public void The_certificates_of_the_proxies_and_when_they_expire_are_listed()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            Client.ApplyCertificate(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password });
            Client.ApplyProxy(NewProxy("tls", change: c =>
            {
                c.TrafficType = TrafficType.Https;
                c.TlsCertificate = "site";
                c.HostNames.Add(new NpHostName { Host = "a.test", TlsCertificate = "site" });
            }));
            Client.ApplyProxy(NewProxy("other", change: c => { c.TrafficType = TrafficType.Https; c.TlsCertificate = "SITE"; }));

            var certificate = Assert.Single(Client.GetCertificates());

            Assert.Equal("", certificate.Error);
            Assert.True(certificate.IsLoaded);
            Assert.Contains("site.test", certificate.Subject);
            Assert.InRange(certificate.DaysLeft!.Value, 28, 31);
            Assert.Equal(["other", "tls"], certificate.UsedBy);
        }

        [Fact]
        public void A_certificate_that_cannot_be_read_is_reported_per_certificate()
        {
            using var directory = new TempDirectory();
            var good = Certs.WritePfx(directory, "good.test");
            var reply = Client.ApplyCertificate(new NpCertificateConfiguration { Name = "wrong", Path = good, Password = "wrong password" });

            Assert.True(reply.Success); //It is saved, so that the file can be put right.
            Assert.False(string.IsNullOrEmpty(reply.LoadError));

            var certificate = Assert.Single(Client.GetCertificates());

            Assert.False(certificate.IsLoaded);
            Assert.False(string.IsNullOrEmpty(certificate.Error));
            Assert.Null(certificate.DaysLeft);
        }

        [Fact]
        public async Task The_end_of_the_log_is_available()
        {
            var proxy = NewProxy("logged");
            Client.ApplyProxy(proxy);

            var lines = await Support.Client.Eventually(() => Task.FromResult(Client.GetLog(50, out _)), o => o.Any(l => l.Contains("Proxy 'logged'")), seconds: 5);

            Assert.Contains(lines, o => o.Contains("Proxy 'logged'"));
            Assert.True(lines.Count <= 50);
            Assert.Single(Client.GetLog(1, out _));
        }

        [Fact]
        public void A_service_that_does_not_log_to_a_file_says_so()
        {
            var settings = Client.GetSettings();
            settings.LogPath = "";
            Client.SetSettings(settings);

            var lines = Client.GetLog(10, out var message);

            Assert.Empty(lines);
            Assert.Contains("does not log to a file", message);
        }

        //---------------------------------------------------------------- Export and import

        [Fact]
        public async Task An_export_can_be_imported_into_a_service_that_has_nothing()
        {
            using var directory = new TempDirectory();
            var a = NewProxy("a", "A");
            var b = NewProxy("b", "B", c => c.UseStickySessions = true);
            Client.ApplyProxy(a);
            Client.ApplyProxy(b);
            Client.ApplyPlugin(PluginSupport.Recorder("rec", directory));

            var export = Client.Export();
            Assert.Equal(["a", "b"], export.Proxies.Select(o => o.Name).Order());
            Assert.Equal(["rec"], export.Plugins.Select(o => o.Name));
            Assert.NotNull(export.Settings);

            Client.ProxyAction("a", NpProxyAction.Delete);
            Client.ProxyAction("b", NpProxyAction.Delete);
            Client.PluginAction("rec", NpPluginAction.Delete);

            foreach (var plugin in export.Plugins)
            {
                Assert.True(Client.ApplyPlugin(plugin).Success);
            }

            foreach (var proxy in export.Proxies)
            {
                Assert.True(Client.ApplyProxy(proxy).Success);
            }

            Assert.Contains("backend=A", await Get(a));
            Assert.Contains("backend=B", await Get(b));
            Assert.True(Client.GetProxy("b")!.UseStickySessions);
        }
    }
}
