using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// Exporting the whole configuration as a backup, and importing one in place of the current configuration.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class BackupTests(ManagementFixture service) : ManagementTest(service)
    {
        private static string ProxyFile => Path.Combine(TestEnvironment.ConfigurationFolder, "NpProxyConfiguration.json");

        private static NpConfigurationBackup Parse(string document) => JsonConvert.DeserializeObject<NpConfigurationBackup>(document)!;

        private static string Edit(string document, Action<JObject> change)
        {
            var json = JObject.Parse(document);
            change(json);
            return json.ToString();
        }

        private void AssertUntouched(params string[] proxyNames)
        {
            Assert.Equal(proxyNames.Order(), Client.ListProxies().Select(o => o.Name).Order());
            Assert.All(Client.ListProxies(), o => Assert.True(o.IsRunning));
        }

        [Fact]
        public void The_export_holds_everything_the_service_is_set_up_with()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            Client.ApplyCertificate(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password });
            Client.ApplyProxy(NewProxy("web", change: c => { c.TrafficType = TrafficType.Https; c.TlsCertificate = "site"; }));
            Client.AddUser("bob", "bob-pass", "a user");

            var backup = Parse(Client.ExportConfiguration());

            Assert.Equal(NpConfigurationBackup.CurrentFormatVersion, backup.FormatVersion);
            Assert.Equal(Service.Port, backup.ManagementPort);
            Assert.Equal(["web"], backup.Proxies.Select(o => o.Name));
            Assert.Equal("site", backup.Proxies[0].TlsCertificate);
            Assert.Equal(["site"], backup.Certificates.Select(o => o.Name));
            Assert.Equal(Certs.Password, backup.Certificates[0].Password);
            Assert.Contains(backup.Users, o => o.UserName == "bob" && o.PasswordHash == NetProxy.Library.Utilities.NpUtility.Sha256("bob-pass"));
            Assert.Contains(backup.Users, o => o.UserName == ManagementFixture.UserName);
            Assert.Equal(Service.LogPath, backup.Settings.LogPath);
            Assert.Contains("\"Https\"", Client.ExportConfiguration()); //Enums are names, which is readable.
        }

        [Fact]
        public async Task An_import_puts_the_exported_configuration_back_in_place_of_what_was_changed_since()
        {
            var original = NewProxy("keep", "KEEP");
            Client.ApplyProxy(original);
            Client.ApplyProxy(NewProxy("change-me", "OLD"));
            Client.AddUser("bob", "bob-pass");
            var document = Client.ExportConfiguration();

            //Changes after the backup.
            Client.ProxyAction("keep", NpProxyAction.Delete);
            var changed = Client.GetProxy("change-me")!;
            changed.Description = "edited later";
            Client.ApplyProxy(changed);
            Client.ApplyProxy(NewProxy("added-later"));
            Client.RemoveUser("bob");

            var reply = Client.ImportConfiguration(document);

            Assert.True(reply.Success, string.Join("; ", reply.Problems));
            Assert.Empty(reply.Problems);
            Assert.Equal(2, reply.Proxies);
            AssertUntouched("keep", "change-me");
            Assert.Equal("", Client.GetProxy("change-me")!.Description);
            Assert.Contains(Client.ListUsers(), o => o.UserName == "bob");
            Assert.Contains("backend=KEEP", await Get(original));
        }

        [Fact]
        public void An_import_replaces_the_files_on_disk_too()
        {
            Client.ApplyProxy(NewProxy("one"));
            var document = Client.ExportConfiguration();
            Client.ApplyProxy(NewProxy("two"));

            Assert.True(Client.ImportConfiguration(document).Success);

            var saved = File.ReadAllText(ProxyFile);
            Assert.Contains("\"one\"", saved);
            Assert.DoesNotContain("\"two\"", saved);
        }

        [Fact]
        public void Only_the_proxies_that_start_automatically_are_started_and_the_rest_are_stopped()
        {
            Client.ApplyProxy(NewProxy("auto"));
            Client.ApplyProxy(NewProxy("manual", change: c => c.AutoStart = false));
            Client.ProxyAction("manual", NpProxyAction.Start); //Running now, but it is not set to start by itself.
            var document = Client.ExportConfiguration();

            Assert.True(Client.ImportConfiguration(document).Success);

            var proxies = Client.ListProxies().ToDictionary(o => o.Name);
            Assert.True(proxies["auto"].IsRunning);
            Assert.False(proxies["manual"].IsRunning);
        }

        [Fact]
        public void Proxies_that_are_not_in_the_file_are_stopped_and_gone()
        {
            var document = Client.ExportConfiguration(); //Nothing in it but the user.
            var stray = NewProxy("stray");
            Client.ApplyProxy(stray);
            var running = Service.Manager.FindProxy(null, "stray")!;
            Assert.True(running.IsRunning);

            Assert.True(Client.ImportConfiguration(document).Success);

            Assert.Empty(Client.ListProxies());
            Assert.False(running.IsRunning);
        }

        [Theory]
        [InlineData("")]
        [InlineData("this is not json")]
        [InlineData("[1,2,3]")]
        public void A_file_that_is_not_a_configuration_changes_nothing(string document)
        {
            Client.ApplyProxy(NewProxy("web"));

            var reply = Client.ImportConfiguration(document);

            Assert.False(reply.Success);
            Assert.NotEmpty(reply.Problems);
            AssertUntouched("web");
        }

        [Fact]
        public void A_configuration_that_is_not_good_changes_nothing()
        {
            Client.ApplyProxy(NewProxy("web"));
            var document = Client.ExportConfiguration();

            var noUsers = Client.ImportConfiguration(Edit(document, o => o["Users"] = new JArray()));
            Assert.False(noUsers.Success);
            Assert.Contains(noUsers.Problems, o => o.Contains("at least one user"));

            var newer = Client.ImportConfiguration(Edit(document, o => o["FormatVersion"] = 99));
            Assert.False(newer.Success);
            Assert.Contains(newer.Problems, o => o.Contains("format 99"));

            var unknownCertificate = Client.ImportConfiguration(Edit(document, o =>
            {
                o["Proxies"]![0]!["TrafficType"] = "Https";
                o["Proxies"]![0]!["TlsCertificate"] = "nobody";
            }));
            Assert.False(unknownCertificate.Success);
            Assert.Contains(unknownCertificate.Problems, o => o.Contains("'nobody' is not set up"));

            var noPort = Client.ImportConfiguration(Edit(document, o => o["ManagementPort"] = 0));
            Assert.False(noPort.Success);

            AssertUntouched("web");
        }

        [Fact]
        public void Two_proxies_that_cannot_share_a_port_are_refused_before_anything_is_changed()
        {
            Client.ApplyProxy(NewProxy("web"));
            var document = Client.ExportConfiguration();

            var reply = Client.ImportConfiguration(Edit(document, o =>
            {
                var copy = (JObject)o["Proxies"]![0]!.DeepClone();
                copy["Name"] = "twin";
                copy["Id"] = Guid.NewGuid();
                copy["TrafficType"] = "Tcp";
                o["Proxies"]![0]!["TrafficType"] = "Tcp";
                ((JArray)o["Proxies"]!).Add(copy);
            }));

            Assert.False(reply.Success);
            Assert.NotEmpty(reply.Problems);
            AssertUntouched("web");
        }

        [Fact]
        public void A_dry_run_checks_and_counts_but_changes_nothing()
        {
            Client.ApplyProxy(NewProxy("one"));
            var document = Client.ExportConfiguration();
            Client.ApplyProxy(NewProxy("two"));

            var reply = Client.ImportConfiguration(document, dryRun: true);

            Assert.True(reply.Success);
            Assert.Equal(1, reply.Proxies);
            Assert.Null(reply.PreviousConfigurationFile);
            AssertUntouched("one", "two");
        }

        [Fact]
        public void The_configuration_that_was_replaced_is_kept_on_the_service_machine()
        {
            Client.ApplyProxy(NewProxy("before"));
            var document = Edit(Client.ExportConfiguration(), o => o["Proxies"] = new JArray());

            var reply = Client.ImportConfiguration(document);

            Assert.True(reply.Success);
            Assert.Empty(Client.ListProxies());
            Assert.NotNull(reply.PreviousConfigurationFile);
            Assert.Contains("\"before\"", File.ReadAllText(reply.PreviousConfigurationFile!));

            //And that copy can be imported to go back.
            Assert.True(Client.ImportConfiguration(File.ReadAllText(reply.PreviousConfigurationFile!)).Success);
            AssertUntouched("before");
        }

        [Fact]
        public void A_proxy_that_cannot_start_is_reported_and_the_rest_of_the_configuration_is_in_place()
        {
            var taken = new TcpListener(IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var port = ((IPEndPoint)taken.LocalEndpoint).Port;
                Client.ApplyProxy(NewProxy("fine"));
                var document = Edit(Client.ExportConfiguration(), o =>
                {
                    var blocked = (JObject)o["Proxies"]![0]!.DeepClone();
                    blocked["Name"] = "blocked";
                    blocked["Id"] = Guid.NewGuid();
                    blocked["ListenPort"] = port;
                    blocked["TrafficType"] = "Tcp";
                    ((JArray)o["Proxies"]!).Add(blocked);
                });

                var reply = Client.ImportConfiguration(document);

                Assert.True(reply.Success);
                Assert.Contains(reply.Problems, o => o.Contains("'blocked' could not be started"));
                var proxies = Client.ListProxies().ToDictionary(o => o.Name);
                Assert.True(proxies["fine"].IsRunning);
                Assert.False(proxies["blocked"].IsRunning);
            }
            finally
            {
                taken.Stop();
            }
        }

        [Fact]
        public void Certificates_and_users_are_replaced_too()
        {
            using var directory = new TempDirectory();
            var pfx = Certs.WritePfx(directory, "site.test");
            var document = Client.ExportConfiguration(); //Without any certificate.
            Client.ApplyCertificate(new NpCertificateConfiguration { Name = "site", Path = pfx, Password = Certs.Password });
            Client.AddUser("temp", "x");

            Assert.True(Client.ImportConfiguration(document).Success);

            Assert.Empty(Client.GetCertificates());
            Assert.DoesNotContain(Client.ListUsers(), o => o.UserName == "temp");
        }

        [Fact]
        public void The_connection_that_imports_stays_logged_in()
        {
            var document = Client.ExportConfiguration();

            Assert.True(Client.ImportConfiguration(document).Success);

            Assert.Equal(Service.Port, Client.GetInfo().ManagementPort);
        }
    }
}
