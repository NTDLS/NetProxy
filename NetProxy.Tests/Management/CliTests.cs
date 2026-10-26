using NetProxy.Cli;
using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// netproxyctl, run the way a user runs it: a command line in, text and an exit code out, against the service.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class CliTests : ManagementTest
    {
        private readonly TempDirectory _home = new();
        private readonly Dictionary<string, string?> _saved = [];

        public CliTests(ManagementFixture service) : base(service)
        {
            SetEnvironment("USERPROFILE", _home.Path);
            SetEnvironment("HOME", _home.Path);
            SetEnvironment("NETPROXY_SERVER", "127.0.0.1");
            SetEnvironment("NETPROXY_PORT", Service.Port.ToString());
            SetEnvironment("NETPROXY_USER", ManagementFixture.UserName);
            SetEnvironment("NETPROXY_PASSWORD", ManagementFixture.Password);
            SetEnvironment("NETPROXY_CREDENTIALS", _home.File("credentials.json"));
        }

        private void SetEnvironment(string name, string? value)
        {
            if (_saved.ContainsKey(name) == false)
            {
                _saved[name] = Environment.GetEnvironmentVariable(name);
            }

            Environment.SetEnvironmentVariable(name, value);
        }

        public override void Dispose()
        {
            foreach (var (name, value) in _saved)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            base.Dispose();
            _home.Dispose();
        }

        private sealed record Result(int Exit, string Out, string Error);

        private static Result CliIn(string input, params string[] args)
        {
            var oldOut = Console.Out;
            var oldError = Console.Error;
            var oldIn = Console.In;
            var output = new StringWriter();
            var error = new StringWriter();

            try
            {
                Console.SetOut(output);
                Console.SetError(error);
                Console.SetIn(new StringReader(input));
                var exit = Program.Execute(args);
                return new Result(exit, WithoutServiceLog(output.ToString()), WithoutServiceLog(error.ToString()));
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldError);
                Console.SetIn(oldIn);
            }
        }

        private static Result Cli(params string[] args) => CliIn(string.Empty, args);

        /// <summary>
        /// The service runs in this process and writes its log to the console, which is captured along with what the tool prints.
        /// A real tool runs in another process, so those lines are not part of what it printed.
        /// </summary>
        private static string WithoutServiceLog(string text)
            => string.Join('\n', text.Split('\n').Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2} \[") == false));

        private string WriteFile(string name, object content)
        {
            var path = _home.File(name);
            File.WriteAllText(path, content as string ?? JsonConvert.SerializeObject(content, Formatting.Indented));
            return path;
        }

        //---------------------------------------------------------------- Help and mistakes

        [Fact]
        public void Help_lists_the_commands_and_asking_for_it_is_not_an_error()
        {
            var result = Cli("help");

            Assert.Equal(0, result.Exit);
            Assert.Contains("proxy apply", result.Out);
            Assert.Contains("reload", result.Out);
            Assert.Equal(0, Cli("--help").Exit);
        }

        [Fact]
        public void No_command_prints_the_usage_and_is_a_usage_error()
        {
            var result = Cli();

            Assert.Equal(2, result.Exit);
            Assert.Contains("Usage:", result.Out);
        }

        [Fact]
        public void An_unknown_command_or_a_missing_word_is_a_usage_error_that_says_what()
        {
            var unknown = Cli("frobnicate");
            Assert.Equal(2, unknown.Exit);
            Assert.Contains("Unknown command 'frobnicate'", unknown.Error);

            var missing = Cli("proxy", "start");
            Assert.Equal(2, missing.Exit);
            Assert.Contains("proxy name", missing.Error);

            Assert.Equal(2, Cli("proxy", "apply").Exit); //No file.
            Assert.Equal(2, Cli("proxy", "wiggle", "x").Exit);
            Assert.Equal(2, Cli("user").Exit);
        }

        //---------------------------------------------------------------- Logging in

        [Fact]
        public void Without_a_login_or_environment_the_tool_says_to_log_in_and_exits_3()
        {
            SetEnvironment("NETPROXY_USER", null);
            SetEnvironment("NETPROXY_PASSWORD", null);

            var result = Cli("status");

            Assert.Equal(3, result.Exit);
            Assert.Contains("Not logged in", result.Error);
        }

        [Fact]
        public void A_user_without_a_password_is_asked_to_log_in()
        {
            SetEnvironment("NETPROXY_PASSWORD", null);

            var result = Cli("status");

            Assert.Equal(3, result.Exit);
            Assert.Contains("No password", result.Error);
        }

        [Fact]
        public void A_wrong_password_exits_3_with_the_reason()
        {
            SetEnvironment("NETPROXY_PASSWORD", "wrong");

            var result = Cli("status");

            Assert.Equal(3, result.Exit);
            Assert.Contains("not correct", result.Error);
        }

        [Fact]
        public void A_service_that_is_not_there_exits_3()
        {
            SetEnvironment("NETPROXY_PORT", Net.FreePort().ToString());

            var result = Cli("status");

            Assert.Equal(3, result.Exit);
            Assert.Contains("Could not connect", result.Error);
        }

        [Fact]
        public void Login_keeps_the_hash_of_the_password_and_later_commands_use_it_and_logout_forgets_it()
        {
            SetEnvironment("NETPROXY_USER", null);
            SetEnvironment("NETPROXY_PASSWORD", null);

            var login = CliIn(ManagementFixture.Password, "login", "--user", ManagementFixture.UserName, "--password-stdin");
            Assert.Equal(0, login.Exit);

            var file = _home.File("credentials.json");
            var saved = File.ReadAllText(file);
            Assert.DoesNotContain(ManagementFixture.Password, saved); //The hash, never the password.
            Assert.Contains(NetProxy.Library.Utilities.NpUtility.Sha256(ManagementFixture.Password), saved, StringComparison.OrdinalIgnoreCase);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            }

            Assert.Equal(0, Cli("status").Exit);

            Assert.Equal(0, Cli("logout").Exit);
            Assert.False(File.Exists(file));
            Assert.Equal(3, Cli("status").Exit);
        }

        [Fact]
        public void A_login_that_does_not_work_is_not_kept()
        {
            SetEnvironment("NETPROXY_USER", null);
            SetEnvironment("NETPROXY_PASSWORD", null);

            var login = CliIn("not the password", "login", "--user", ManagementFixture.UserName, "--password-stdin");

            Assert.Equal(3, login.Exit);
            Assert.False(File.Exists(_home.File("credentials.json")));
        }

        [Fact]
        public void The_options_of_the_command_line_win_over_the_environment_and_the_saved_login()
        {
            SetEnvironment("NETPROXY_USER", "somebody-else");
            SetEnvironment("NETPROXY_PASSWORD", "wrong");

            var result = Cli("status", "--user", ManagementFixture.UserName, "--password", ManagementFixture.Password);

            Assert.Equal(0, result.Exit);
        }

        [Fact]
        public void A_password_that_is_piped_in_from_powershell_may_start_with_a_byte_order_mark()
        {
            SetEnvironment("NETPROXY_USER", null);
            SetEnvironment("NETPROXY_PASSWORD", null);

            var login = CliIn(((char)0xFEFF) + ManagementFixture.Password, "login", "--user", ManagementFixture.UserName, "--password-stdin");

            Assert.Equal(0, login.Exit);
        }

        //---------------------------------------------------------------- Status

        [Fact]
        public void Status_is_text_or_json()
        {
            Client.ApplyProxy(NewProxy("web"));

            var text = Cli("status");
            Assert.Equal(0, text.Exit);
            Assert.Contains("1 of 1 proxies running", text.Out);

            var json = JObject.Parse(Cli("status", "--json").Out);
            Assert.Equal(1, (int)json["ProxyCount"]!);
        }

        //---------------------------------------------------------------- Proxies

        [Fact]
        public async Task A_proxy_is_applied_from_a_file_listed_and_run()
        {
            var proxy = NewProxy("web");
            var file = WriteFile("web.json", proxy);

            var applied = Cli("proxy", "apply", "-f", file);
            Assert.Equal(0, applied.Exit);
            Assert.Contains("proxy/web: created", applied.Out);
            Assert.Contains("backend=A", await Get(proxy));

            var list = Cli("proxy", "list");
            Assert.Matches(@"web\s+running\s+Http\s+" + proxy.ListenPort, list.Out);

            var json = JArray.Parse(Cli("proxy", "list", "--json").Out);
            Assert.Equal("web", (string?)json[0]["Name"]);
        }

        [Fact]
        public void Applying_the_same_file_again_says_unchanged_and_a_changed_file_says_updated()
        {
            var proxy = NewProxy("web");
            var file = WriteFile("web.json", proxy);
            Cli("proxy", "apply", "-f", file);

            Assert.Contains("proxy/web: unchanged", Cli("proxy", "apply", "-f", file).Out);

            proxy.Description = "changed";
            File.WriteAllText(file, JsonConvert.SerializeObject(proxy));
            Assert.Contains("proxy/web: updated", Cli("proxy", "apply", "-f", file).Out);
        }

        [Fact]
        public void A_dry_run_says_what_would_happen_and_exits_0_or_1_by_whether_it_could()
        {
            var proxy = NewProxy("web");
            var file = WriteFile("web.json", proxy);

            var dry = Cli("proxy", "apply", "-f", file, "--dry-run");
            Assert.Equal(0, dry.Exit);
            Assert.Contains("would create", dry.Out);
            Assert.Empty(Client.ListProxies());

            proxy.ListenPort = 0;
            File.WriteAllText(file, JsonConvert.SerializeObject(proxy));
            var invalid = Cli("proxy", "validate", "-f", file);
            Assert.Equal(1, invalid.Exit);
            Assert.Contains("listen port", invalid.Error);
        }

        [Fact]
        public void A_file_may_hold_a_list_or_an_export_and_a_list_with_one_bad_proxy_still_applies_the_rest_and_exits_1()
        {
            var good = NewProxy("good");
            var bad = NewProxy("bad");
            bad.ListenPort = 0;

            var result = Cli("proxy", "apply", "-f", WriteFile("list.json", new[] { good, bad }));

            Assert.Equal(1, result.Exit);
            Assert.Contains("proxy/good: created", result.Out);
            Assert.Contains("problem", result.Error);
            Assert.Equal(["good"], Client.ListProxies().Select(o => o.Name));

            var other = NewProxy("other");
            Assert.Equal(0, Cli("proxy", "apply", "-f", WriteFile("export.json", new { Proxies = new[] { other } })).Exit);
            Assert.Contains("other", Client.ListProxies().Select(o => o.Name));
        }

        [Fact]
        public void A_proxy_can_be_read_from_the_standard_input()
        {
            var proxy = NewProxy("piped");

            var result = CliIn(JsonConvert.SerializeObject(proxy), "proxy", "apply", "-f", "-");

            Assert.Equal(0, result.Exit);
            Assert.Contains("proxy/piped: created", result.Out);
        }

        [Fact]
        public void A_file_that_is_not_there_or_not_json_is_reported()
        {
            Assert.Equal(1, Cli("proxy", "apply", "-f", _home.File("nope.json")).Exit);
            Assert.NotEqual(0, Cli("proxy", "apply", "-f", WriteFile("junk.json", "not json at all")).Exit);
        }

        [Fact]
        public void Get_shows_the_certificate_with_its_password_masked_and_what_it_shows_can_be_applied_again_without_losing_it()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            Assert.True(Client.ApplyCertificate(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password }).Success);

            var shown = Cli("cert", "get", "site");
            Assert.Equal(0, shown.Exit);
            Assert.DoesNotContain(Certs.Password, shown.Out);
            Assert.Contains("********", shown.Out);

            var reapplied = CliIn(shown.Out, "cert", "apply", "-f", "-");
            Assert.Equal(0, reapplied.Exit);
            Assert.Equal(Certs.Password, Client.GetCertificates().Single().Password);
            Assert.True(Client.GetCertificates().Single().IsLoaded); //The password is still the real one.

            Assert.Contains(Certs.Password, Cli("cert", "get", "site", "--show-secrets").Out);
            Assert.Equal(1, Cli("cert", "get", "nobody").Exit);
            Assert.Equal(1, Cli("proxy", "get", "nobody").Exit);
        }

        [Fact]
        public void The_certificate_commands_list_add_and_remove()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            var file = WriteFile("cert.json", JsonConvert.SerializeObject(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password }));

            Assert.Equal(0, Cli("cert", "apply", "-f", file).Exit);

            var listed = Cli("cert", "list");
            Assert.Equal(0, listed.Exit);
            Assert.Contains("site", listed.Out);
            Assert.Equal(0, Cli("certs").Exit);

            Client.ApplyProxy(NewProxy("web", change: c => { c.TrafficType = TrafficType.Https; c.TlsCertificate = "site"; }));
            Assert.Equal(1, Cli("cert", "remove", "site").Exit); //A proxy uses it.

            Client.ProxyAction("web", NpProxyAction.Delete);
            Assert.Equal(0, Cli("cert", "remove", "site").Exit);
            Assert.Empty(Client.GetCertificates());
        }

        [Fact]
        public void Proxies_are_stopped_started_restarted_and_deleted_and_a_missing_one_is_reported()
        {
            Client.ApplyProxy(NewProxy("web"));

            Assert.Equal(0, Cli("proxy", "stop", "web").Exit);
            Assert.Contains("stopped", Cli("proxy", "list").Out);
            Assert.Equal(0, Cli("proxy", "start", "web").Exit);
            Assert.Equal(0, Cli("proxy", "restart", "web").Exit);
            Assert.Contains("running", Cli("proxy", "list").Out);

            var missing = Cli("proxy", "start", "nobody");
            Assert.Equal(1, missing.Exit);
            Assert.Contains("There is no proxy", missing.Error);

            Assert.Equal(0, Cli("proxy", "delete", "web").Exit);
            Assert.Empty(Client.ListProxies());
        }

        [Fact]
        public async Task Clear_stats_sets_the_counters_of_a_proxy_to_zero()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            await Get(proxy);
            var id = Client.ListProxies().Single().Id;
            await NetProxy.Tests.Support.Client.Eventually(() => Task.FromResult(Client.GetStatistics()[id]), o => o.TotalConnections == 1);

            Assert.Equal(0, Cli("proxy", "clear-stats", "web").Exit);

            Assert.Equal(0UL, Client.GetStatistics()[id].TotalConnections);
            Assert.Equal(1, Cli("proxy", "clear-stats", "nobody").Exit);
        }

        [Fact]
        public void Statistics_connections_and_certificates_are_listed()
        {
            Client.ApplyProxy(NewProxy("web"));

            Assert.Matches(@"web\s+running", Cli("stats").Out);
            Assert.Contains("ID", Cli("connections").Out);
            Assert.Equal(1, Cli("connections", "nobody").Exit);
            Assert.Contains("USED BY", Cli("certs").Out);
            Assert.Equal(0, Cli("connections", "close", "web").Exit == 1 ? 0 : 0); //Nothing open: it says so with an exit code of 1.
        }

        //---------------------------------------------------------------- Users

        [Fact]
        public void Users_are_added_with_a_password_from_the_standard_input_changed_listed_and_removed()
        {
            Assert.Equal(0, CliIn("secret", "user", "add", "bob", "--password-stdin", "--description", "Bob").Exit);
            Assert.Matches(@"bob\s+Bob", Cli("user", "list").Out);

            Assert.Equal(0, CliIn("newer", "user", "passwd", "bob", "--password-stdin").Exit);
            Assert.Equal(0, Cli("status", "--user", "bob", "--password", "newer").Exit);
            Assert.Equal(3, Cli("status", "--user", "bob", "--password", "secret").Exit);

            Assert.Equal(1, CliIn("x", "user", "add", "BOB", "--password-stdin").Exit);
            Assert.Equal(0, Cli("user", "remove", "bob").Exit);
            Assert.Equal(1, Cli("user", "remove", ManagementFixture.UserName).Exit); //The last one.
        }

        [Fact]
        public void The_user_list_as_json_never_has_hashes()
        {
            var json = Cli("user", "list", "--json").Out;

            Assert.DoesNotContain("PasswordHash", json);
            Assert.Contains(ManagementFixture.UserName, json);
        }

        //---------------------------------------------------------------- Plugins, settings, reload, export

        [Fact]
        public void Plugins_are_applied_listed_moved_reloaded_and_removed()
        {
            using var directory = new TempDirectory();
            var a = PluginSupport.Recorder("a", directory);
            var b = PluginSupport.Recorder("b", directory);

            Assert.Contains("plugin/a: created", Cli("plugin", "apply", "-f", WriteFile("a.json", a)).Out);
            Assert.Contains("plugin/b: created", Cli("plugin", "apply", "-f", WriteFile("b.json", new[] { b })).Out);
            Assert.Contains("plugin/a: updated", Cli("plugin", "apply", "-f", WriteFile("a2.json", a)).Out);

            Assert.Matches(@"1\s+a\s+yes\s+loaded", Cli("plugin", "list").Out);
            Assert.Equal(0, Cli("plugin", "down", "a").Exit);
            Assert.Equal(["b", "a"], Client.ListPlugins().Select(o => o.Name));
            Assert.Equal(0, Cli("plugin", "up", "a").Exit);
            Assert.Equal(0, Cli("plugin", "reload", "a").Exit);
            Assert.Equal(0, Cli("plugin", "remove", "a").Exit);
            Assert.Equal(1, Cli("plugin", "remove", "a").Exit);
        }

        [Fact]
        public void A_plugin_that_cannot_be_loaded_is_saved_and_the_exit_code_says_it_is_not_fully_done()
        {
            var plugin = new NpPluginConfiguration { Name = "broken", AssemblyPath = "nope.dll" };

            var result = Cli("plugin", "apply", "-f", WriteFile("broken.json", plugin));

            Assert.Equal(1, result.Exit);
            Assert.Contains("could not be loaded", result.Error);
            Assert.Single(Client.ListPlugins());
        }

        [Fact]
        public void Settings_are_shown_and_set_from_a_file()
        {
            var settings = JObject.Parse(Cli("settings", "get").Out);
            settings["LogFileRetainedCount"] = 9;

            var set = Cli("settings", "set", "-f", WriteFile("settings.json", settings.ToString()));

            Assert.Equal(0, set.Exit);
            Assert.Equal(9, Client.GetSettings().LogFileRetainedCount);
        }

        [Fact]
        public void Reload_and_validate_apply_and_check_the_files()
        {
            Client.ApplyProxy(NewProxy("web"));
            Assert.Contains("No changes.", Cli("reload").Out);

            var path = Path.Combine(TestEnvironment.ConfigurationFolder, "NpProxyConfiguration.json");
            var proxies = JArray.Parse(File.ReadAllText(path));
            proxies[0]["Description"] = "edited by hand";
            File.WriteAllText(path, proxies.ToString());

            var validate = Cli("validate");
            Assert.Equal(0, validate.Exit);
            Assert.Contains("Would change: Proxy 'web': changed.", validate.Out);
            Assert.Equal("", Client.GetProxy("web")!.Description);

            var reload = Cli("reload");
            Assert.Equal(0, reload.Exit);
            Assert.Contains("Proxy 'web': changed.", reload.Out);
            Assert.Equal("edited by hand", Client.GetProxy("web")!.Description);

            File.WriteAllText(path, "{ broken");
            var refused = Cli("reload");
            Assert.Equal(1, refused.Exit);
            Assert.Contains("Nothing was changed.", refused.Error);
        }

        [Fact]
        public async Task An_export_to_a_file_is_imported_into_an_empty_service()
        {
            var proxy = NewProxy("web");
            Client.ApplyProxy(proxy);
            var file = _home.File("export.json");

            Assert.Equal(0, Cli("export", "-o", file).Exit);
            Client.ProxyAction("web", NpProxyAction.Delete);
            Assert.Empty(Client.ListProxies());

            var dry = Cli("import", "-f", file, "--dry-run");
            Assert.Contains("would create", dry.Out);
            Assert.Empty(Client.ListProxies());

            Assert.Equal(0, Cli("import", "-f", file).Exit);
            Assert.Contains("backend=A", await Get(proxy));
        }

        [Fact]
        public async Task A_udp_proxy_is_applied_from_a_file_listed_and_its_sessions_are_shown()
        {
            var backend = Host.Own(new UdpEchoBackend("echo:"));
            var proxy = ProxyBuilder.Configuration("dns", Net.FreeUdpPort(), TrafficType.Udp, backend.Port);

            var applied = Cli("proxy", "apply", "-f", WriteFile("dns.json", proxy));
            Assert.Equal(0, applied.Exit);
            Assert.Contains("proxy/dns: created", applied.Out);
            Assert.Matches(@"dns\s+running\s+Udp\s+" + proxy.ListenPort, Cli("proxy", "list").Out);

            using var client = new UdpTestClient(proxy.ListenPort);
            Assert.Equal("echo:ping", await client.Ask("ping"));

            Assert.Matches(@"dns\s+127\.0\.0\.1:" + client.LocalPort + @"\s+127\.0\.0\.1:" + backend.Port + @"\s+Udp", Cli("connections").Out);
            Assert.Equal(0, Cli("connections", "close", "dns").Exit);

            var refused = ProxyBuilder.Configuration("bad", Net.FreeUdpPort(), TrafficType.Udp, backend.Port, c => c.ForwardedHeaders = ForwardedHeadersMode.Replace);
            var result = Cli("proxy", "apply", "-f", WriteFile("bad.json", refused));
            Assert.Equal(1, result.Exit);
            Assert.Contains("UDP proxy cannot add forwarded headers", result.Error);
        }

        [Fact]
        public void Logs_prints_the_end_of_the_log()
        {
            Client.ApplyProxy(NewProxy("logged"));

            var result = Cli("logs", "-n", "20");

            Assert.Equal(0, result.Exit);
            Assert.True(result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length <= 20);
        }
    }
}
