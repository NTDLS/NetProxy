using NetProxy.Library;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Management
{
    /// <summary>
    /// The certificates of the service: set up once, with a name, and used by the proxies by that name.
    /// </summary>
    [Collection(ManagementCollection.Name)]
    public class CertificateTests(ManagementFixture service) : ManagementTest(service)
    {
        private string AddCertificate(TempDirectory directory, string name, string commonName)
        {
            var pfx = Certs.WritePfx(directory, commonName);
            var reply = Client.ApplyCertificate(new NpCertificateConfiguration { Name = name, Path = pfx, Password = Certs.Password });
            Assert.True(reply.Success, reply.Message);
            return pfx;
        }

        private NpProxyConfiguration HttpsProxy(string name, string certificate, string backendName = "A")
            => NewProxy(name, backendName, c =>
            {
                c.TrafficType = TrafficType.Https;
                c.TlsCertificate = certificate;
            });

        [Fact]
        public void A_certificate_is_added_listed_and_replaced_by_name()
        {
            using var directory = new TempDirectory();
            var first = AddCertificate(directory, "site", "one.test");

            var certificate = Assert.Single(Client.GetCertificates());
            Assert.Equal("site", certificate.Name);
            Assert.Equal(first, certificate.Path);
            Assert.True(certificate.IsLoaded);
            Assert.Contains("one.test", certificate.Subject);

            var second = Certs.WritePfx(directory, "two.test");
            var reply = Client.ApplyCertificate(new NpCertificateConfiguration { Name = "SITE", Path = second, Password = Certs.Password });

            Assert.True(reply.Success);
            Assert.Equal("updated", reply.Action);
            certificate = Assert.Single(Client.GetCertificates());
            Assert.Contains("two.test", certificate.Subject);
        }

        [Fact]
        public void A_certificate_needs_a_name_and_a_file_and_names_are_unique()
        {
            using var directory = new TempDirectory();
            var pfx = AddCertificate(directory, "site", "one.test");

            Assert.False(Client.ApplyCertificate(new NpCertificateConfiguration { Name = "", Path = pfx }).Success);
            Assert.False(Client.ApplyCertificate(new NpCertificateConfiguration { Name = "other", Path = "" }).Success);

            //Applying by name replaces the certificate with that name; a new certificate with a name that is taken is refused.
            var clash = NetProxy.Service.Singletons.Certificates.Upsert(
                new NpCertificateConfiguration { Id = Guid.NewGuid(), Name = "Site", Path = pfx, Password = Certs.Password }, out _, out _);
            Assert.Contains("already a certificate named", clash);
            Assert.Single(Client.GetCertificates());
        }

        [Fact]
        public void A_file_that_cannot_be_loaded_is_saved_and_the_reason_is_reported()
        {
            using var directory = new TempDirectory();
            var broken = directory.File("broken.pfx");
            File.WriteAllBytes(broken, [1, 2, 3]);

            var reply = Client.ApplyCertificate(new NpCertificateConfiguration { Name = "broken", Path = broken });

            Assert.True(reply.Success);
            Assert.False(string.IsNullOrEmpty(reply.LoadError));
            Assert.False(Client.GetCertificates().Single().IsLoaded);
        }

        [Fact]
        public void A_proxy_cannot_use_a_certificate_that_is_not_set_up()
        {
            var result = Client.ApplyProxy(HttpsProxy("tls", "nobody"));

            Assert.False(result.Success);
            Assert.Contains(result.Problems, o => o.Contains("'nobody' is not set up", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(Client.ListProxies());
        }

        [Fact]
        public void A_proxy_that_uses_a_certificate_is_started_with_it_and_a_host_name_can_use_another()
        {
            using var directory = new TempDirectory();
            AddCertificate(directory, "main", "main.test");
            AddCertificate(directory, "www", "www.test");

            var proxy = HttpsProxy("tls", "main");
            proxy.HostNames.Add(new NpHostName { Host = "www.test", TlsCertificate = "www" });
            var result = Client.ApplyProxy(proxy);

            Assert.True(result.Success, string.Join("; ", result.Problems));
            Assert.Null(result.StartError);
            Assert.True(Service.Manager.FindProxy(null, "tls")!.IsRunning);
        }

        [Fact]
        public void A_certificate_that_proxies_use_cannot_be_deleted_until_they_stop_using_it()
        {
            using var directory = new TempDirectory();
            AddCertificate(directory, "site", "site.test");
            Client.ApplyProxy(HttpsProxy("a", "site"));
            Client.ApplyProxy(HttpsProxy("b", "site", "B"));

            var refused = Client.DeleteCertificate("site");

            Assert.NotNull(refused);
            Assert.Contains("'a'", refused);
            Assert.Contains("'b'", refused);
            Assert.Single(Client.GetCertificates());

            Client.ProxyAction("a", NpProxyAction.Delete);
            Client.ProxyAction("b", NpProxyAction.Delete);

            Assert.Null(Client.DeleteCertificate("site"));
            Assert.Empty(Client.GetCertificates());
            Assert.NotNull(Client.DeleteCertificate("site")); //There is none now.
        }

        [Fact]
        public void Renaming_a_certificate_renames_it_in_the_proxies_that_use_it()
        {
            using var directory = new TempDirectory();
            AddCertificate(directory, "old", "site.test");
            var proxy = HttpsProxy("tls", "old");
            proxy.HostNames.Add(new NpHostName { Host = "a.test", TlsCertificate = "old" });
            Client.ApplyProxy(proxy);

            var existing = Client.GetCertificates().Single();
            var reply = Client.ApplyCertificate(new NpCertificateConfiguration { Id = existing.Id, Name = "renamed", Path = existing.Path, Password = existing.Password });

            Assert.True(reply.Success);
            var stored = Client.GetProxy("tls")!;
            Assert.Equal("renamed", stored.TlsCertificate);
            Assert.Equal("renamed", stored.HostNames[0].TlsCertificate);
            Assert.Equal(["tls"], Client.GetCertificates().Single().UsedBy);
            Assert.True(Service.Manager.FindProxy(null, "tls")!.IsRunning);
        }

        [Fact]
        public async Task Pointing_a_certificate_at_another_file_is_used_by_the_running_proxies_without_a_restart()
        {
            using var directory = new TempDirectory();
            AddCertificate(directory, "site", "one.test");
            var proxy = HttpsProxy("tls", "site");
            Client.ApplyProxy(proxy);
            var running = Service.Manager.FindProxy(null, "tls");

            Assert.Equal("one.test", (await NetProxy.Tests.Support.Client.Https(proxy.ListenPort, "one.test")).CertificateName);

            var existing = Client.GetCertificates().Single();
            Client.ApplyCertificate(new NpCertificateConfiguration { Id = existing.Id, Name = "site", Path = Certs.WritePfx(directory, "two.test"), Password = Certs.Password });

            Assert.Same(running, Service.Manager.FindProxy(null, "tls"));
            Assert.Equal("two.test", (await NetProxy.Tests.Support.Client.Https(proxy.ListenPort, "two.test")).CertificateName);
        }

        [Fact]
        public void The_export_holds_the_certificates()
        {
            using var directory = new TempDirectory();
            var pfx = AddCertificate(directory, "site", "site.test");

            var export = Client.Export();

            var certificate = Assert.Single(export.Certificates);
            Assert.Equal("site", certificate.Name);
            Assert.Equal(pfx, certificate.Path);
            Assert.Equal(Certs.Password, certificate.Password);
        }
    }
}
