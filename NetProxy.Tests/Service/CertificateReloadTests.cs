using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Proxy;
using NetProxy.Tests.Support;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    /// <summary>
    /// The PFX files are watched: a renewed certificate is used for new connections without a restart.
    /// </summary>
    public class CertificateReloadTests
    {
        private static async Task<string> CertificateSeen(int port)
        {
            try
            {
                return (await Client.Https(port, "site.test")).CertificateName;
            }
            catch (Exception)
            {
                return "(failed)";
            }
        }

        private static Task<string> WaitFor(int port, string certificate, double seconds = 20)
            => Client.Eventually(() => CertificateSeen(port), o => o == certificate, seconds);

        private static NpProxyConfiguration Https(int port, int backendPort, string pfx)
            => ProxyBuilder.Configuration("tls", port, TrafficType.Https, backendPort, c =>
            {
                c.TlsCertificatePath = pfx;
                c.TlsCertificatePassword = Certs.Password;
            });

        [Fact]
        public async Task A_certificate_file_that_is_written_over_is_picked_up()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            host.Start(Https(port, host.Backend("A").Port, pfx));
            Assert.Equal("one.test", await CertificateSeen(port));

            File.WriteAllBytes(pfx, Certs.PfxBytes("two.test"));

            Assert.Equal("two.test", await WaitFor(port, "two.test"));
        }

        [Fact]
        public async Task A_certificate_file_that_is_replaced_by_a_rename_is_picked_up()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            host.Start(Https(port, host.Backend("A").Port, pfx));

            var next = host.Directory.File("site.pfx.new");
            File.WriteAllBytes(next, Certs.PfxBytes("two.test"));
            File.Move(next, pfx, overwrite: true);

            Assert.Equal("two.test", await WaitFor(port, "two.test"));
        }

        [Fact]
        public async Task A_connection_that_is_open_keeps_working_when_the_certificate_changes()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            host.Start(Https(port, host.Backend("A").Port, pfx));

            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync("site.test");

            File.WriteAllBytes(pfx, Certs.PfxBytes("two.test"));
            Assert.Equal("two.test", await WaitFor(port, "two.test"));

            await ssl.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n"));
            var buffer = new byte[4096];
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var text = new StringBuilder();
            while (text.ToString().Contains(" end") == false)
            {
                text.Append(Encoding.ASCII.GetString(buffer, 0, await ssl.ReadAsync(buffer, timeout.Token)));
            }

            Assert.Contains("backend=A", text.ToString());
        }

        [Fact]
        public async Task A_file_that_cannot_be_used_leaves_the_previous_certificate_in_place_until_it_is_fixed()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            host.Start(Https(port, host.Backend("A").Port, pfx));

            File.WriteAllBytes(pfx, [1, 2, 3, 4]);
            await Task.Delay(TimeSpan.FromSeconds(6));

            Assert.Equal("one.test", await CertificateSeen(port));

            File.WriteAllBytes(pfx, Certs.PfxBytes("three.test"));
            Assert.Equal("three.test", await WaitFor(port, "three.test"));
        }

        [Fact]
        public async Task A_file_with_the_wrong_password_leaves_the_previous_certificate_in_place()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            var configuration = Https(port, host.Backend("A").Port, pfx);
            host.Start(configuration);

            using var other = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=other.test", other,
                System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            File.WriteAllBytes(pfx, certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "a different password"));
            await Task.Delay(TimeSpan.FromSeconds(6));

            Assert.Equal("one.test", await CertificateSeen(port));
        }

        [Fact]
        public async Task The_certificates_of_host_names_are_watched_too()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("host.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("host-one.test"));
            host.Start(ProxyBuilder.Configuration("tls", port, TrafficType.Https, host.Backend("A").Port, c =>
                c.HostNames.Add(new NpHostName { Host = "site.test", TlsCertificatePath = pfx, TlsCertificatePassword = Certs.Password })));
            Assert.Equal("host-one.test", await CertificateSeen(port));

            File.WriteAllBytes(pfx, Certs.PfxBytes("host-two.test"));

            Assert.Equal("host-two.test", await WaitFor(port, "host-two.test"));
        }

        [Fact]
        public async Task Only_the_changed_file_matters_and_the_other_hosts_keep_working()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var a = host.Directory.File("a.pfx");
            var b = host.Directory.File("b.pfx");
            File.WriteAllBytes(a, Certs.PfxBytes("a-one.test"));
            File.WriteAllBytes(b, Certs.PfxBytes("b-one.test"));
            host.Start(ProxyBuilder.Configuration("tls", port, TrafficType.Https, host.Backend("A").Port, c =>
            {
                c.HostNames.Add(new NpHostName { Host = "a.test", TlsCertificatePath = a, TlsCertificatePassword = Certs.Password });
                c.HostNames.Add(new NpHostName { Host = "b.test", TlsCertificatePath = b, TlsCertificatePassword = Certs.Password });
            }));

            File.WriteAllBytes(b, Certs.PfxBytes("b-two.test"));

            Assert.Equal("b-two.test", await Client.Eventually(async () => (await Client.Https(port, "b.test")).CertificateName, o => o == "b-two.test"));
            Assert.Equal("a-one.test", (await Client.Https(port, "a.test")).CertificateName);
        }

        [Fact]
        public async Task A_stopped_proxy_lets_go_of_its_files()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var pfx = host.Directory.File("site.pfx");
            File.WriteAllBytes(pfx, Certs.PfxBytes("one.test"));
            var proxy = host.Start(Https(port, host.Backend("A").Port, pfx));

            proxy.Stop();

            //The file can be deleted, which it cannot if something still has it open.
            File.Delete(pfx);
            Assert.False(File.Exists(pfx));
            Assert.True(proxy.Start(out var error) == false && error!.Contains("site.pfx"));
        }
    }
}
