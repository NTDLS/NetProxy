using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Tests.Support;
using System.Net.Sockets;
using Xunit;

namespace NetProxy.Tests.Service
{
    /// <summary>
    /// Several proxies on one port, told apart by the host name the client asks for.
    /// </summary>
    public class PortSharingTests
    {
        private static NpProxyConfiguration Proxy(string name, int port, TrafficType type, EchoBackend backend, string? pfx = null, params string[] hosts)
            => ProxyBuilder.Configuration(name, port, type, backend.Port, c =>
            {
                c.TlsCertificatePath = pfx ?? string.Empty;
                c.TlsCertificatePassword = Certs.Password;
                foreach (var host in hosts)
                {
                    c.HostNames.Add(new NpHostName { Host = host });
                }
            });

        //---------------------------------------------------------------- Plain HTTP

        [Fact]
        public async Task Requests_go_to_the_proxy_that_has_the_host_name()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: ["b.example.com", "*.wild.example.com"]));

            Assert.Contains("backend=A", await Client.Http(port, "a.example.com"));
            Assert.Contains("backend=B", await Client.Http(port, "b.example.com"));
            Assert.Contains("backend=B", await Client.Http(port, "deep.sub.wild.example.com"));
        }

        [Fact]
        public async Task The_host_is_not_case_sensitive_and_may_carry_a_port()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: "b.example.com"));

            Assert.Contains("backend=A", await Client.Http(port, "A.EXAMPLE.com:8080"));
        }

        [Fact]
        public async Task The_request_reaches_the_endpoint_whole_and_with_the_original_host()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: "b.example.com"));

            var response = await Client.Http(port, "a.example.com", path: "/some/path?x=1");

            Assert.Contains("host=[a.example.com]", response);
            Assert.Contains("path=[/some/path?x=1]", response);
        }

        [Fact]
        public async Task An_unknown_host_gets_a_404_when_there_is_no_default()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: "b.example.com"));

            Assert.StartsWith("HTTP/1.1 404", await Client.Http(port, "nobody.example.com"));
        }

        [Fact]
        public async Task The_proxy_without_host_names_gets_what_nobody_claims()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("default", port, TrafficType.Http, host.Backend("DEFAULT")));

            Assert.Contains("backend=DEFAULT", await Client.Http(port, "nobody.example.com"));
            Assert.Contains("backend=A", await Client.Http(port, "a.example.com"));
        }

        [Fact]
        public async Task A_proxy_without_host_names_alone_on_its_port_answers_every_host()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A")));

            Assert.Contains("backend=A", await Client.Http(port, "anything.else"));
            Assert.Contains("backend=A", await Client.Http(port, ""));
        }

        [Fact]
        public async Task A_proxy_with_host_names_answers_only_to_them_even_when_it_is_alone()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));

            Assert.Contains("backend=A", await Client.Http(port, "a.example.com"));
            Assert.StartsWith("HTTP/1.1 404", await Client.Http(port, "another-site.example.com"));
        }

        [Fact]
        public async Task A_proxy_with_host_names_alone_on_a_tls_port_refuses_other_server_names()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var certificate = Certs.WritePfx(host.Directory, "a.test.local");
            host.Start(ProxyBuilder.Configuration("a", port, TrafficType.Https, host.Backend("A").Port, c =>
            {
                c.TlsCertificatePath = certificate;
                c.TlsCertificatePassword = Certs.Password;
                c.HostNames.Add(new NpHostName { Host = "a.test.local" });
            }));

            Assert.Contains("backend=A", (await Client.Https(port, "a.test.local")).Body);
            await Assert.ThrowsAnyAsync<Exception>(() => Client.Https(port, "other.test.local"));
        }

        [Fact]
        public async Task Stopping_one_proxy_leaves_the_others_on_the_port_running()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var a = host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: "b.example.com"));

            a.Stop();

            Assert.Contains("backend=B", await Client.Http(port, "b.example.com"));
            Assert.StartsWith("HTTP/1.1 404", await Client.Http(port, "a.example.com"));

            Assert.True(a.Start(out var error), error);
            Assert.Contains("backend=A", await Client.Http(port, "a.example.com"));
        }

        [Fact]
        public async Task The_port_is_released_when_the_last_proxy_stops()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var a = host.Start(Proxy("a", port, TrafficType.Http, host.Backend("A"), hosts: "a.example.com"));
            var b = host.Start(Proxy("b", port, TrafficType.Http, host.Backend("B"), hosts: "b.example.com"));

            a.Stop();
            b.Stop();

            await Assert.ThrowsAnyAsync<SocketException>(async () => { using var client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port); });

            var again = host.Create(Proxy("again", port, TrafficType.Http, host.Backend("C")));
            Assert.True(again.Start(out var error), error);
        }

        [Fact]
        public void A_proxy_that_conflicts_does_not_start_and_the_others_are_not_disturbed()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var backend = host.Backend("A");
            host.Start(Proxy("a", port, TrafficType.Http, backend, hosts: "a.example.com"));

            Assert.False(host.Create(Proxy("same-host", port, TrafficType.Http, backend, hosts: "a.example.com")).Start(out var duplicate));
            Assert.Contains("a.example.com", duplicate);

            Assert.False(host.Create(Proxy("raw", port, TrafficType.Raw, backend)).Start(out var raw));
            Assert.Contains("HTTP and HTTPS", raw);

            Assert.False(host.Create(Proxy("tls", port, TrafficType.Https, backend, Certs.WritePfx(host.Directory, "x.test"), "x.example.com")).Start(out var mixed));
            Assert.Contains("all HTTP or all HTTPS", mixed);

            host.Start(Proxy("default", port, TrafficType.Http, backend));
            Assert.False(host.Create(Proxy("second-default", port, TrafficType.Http, backend)).Start(out var second));
            Assert.Contains("default", second);
        }

        [Fact]
        public void A_port_that_something_else_uses_is_reported()
        {
            using var host = new TestHost();
            var taken = new TcpListener(System.Net.IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var port = ((System.Net.IPEndPoint)taken.LocalEndpoint).Port;
                var proxy = host.Create(Proxy("a", port, TrafficType.Http, host.Backend("A")));

                Assert.False(proxy.Start(out var error));
                Assert.False(string.IsNullOrEmpty(error));
                Assert.False(proxy.IsRunning);
            }
            finally
            {
                taken.Stop();
            }
        }

        //---------------------------------------------------------------- HTTPS

        [Fact]
        public async Task Each_host_name_gets_its_own_certificate_and_its_own_proxy()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var www = Certs.WritePfx(host.Directory, "www.test.local");
            var apex = Certs.WritePfx(host.Directory, "test.local");

            host.Start(ProxyBuilder.Configuration("www", port, TrafficType.Https, host.Backend("WWW").Port, c =>
                c.HostNames.Add(new NpHostName { Host = "www.test.local", TlsCertificatePath = www, TlsCertificatePassword = Certs.Password })));
            host.Start(ProxyBuilder.Configuration("apex", port, TrafficType.Https, host.Backend("APEX").Port, c =>
                c.HostNames.Add(new NpHostName { Host = "test.local", TlsCertificatePath = apex, TlsCertificatePassword = Certs.Password })));

            var (wwwBody, wwwCertificate) = await Client.Https(port, "www.test.local");
            var (apexBody, apexCertificate) = await Client.Https(port, "test.local");

            Assert.Contains("backend=WWW", wwwBody);
            Assert.Equal("www.test.local", wwwCertificate);
            Assert.Contains("backend=APEX", apexBody);
            Assert.Equal("test.local", apexCertificate);
        }

        [Fact]
        public async Task A_host_without_its_own_certificate_uses_the_certificate_of_the_proxy()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var shared = Certs.WritePfx(host.Directory, "shared.test.local");

            host.Start(ProxyBuilder.Configuration("a", port, TrafficType.Https, host.Backend("A").Port, c =>
            {
                c.TlsCertificatePath = shared;
                c.TlsCertificatePassword = Certs.Password;
                c.HostNames.Add(new NpHostName { Host = "a.test.local" });
            }));

            var (_, certificate) = await Client.Https(port, "a.test.local");
            Assert.Equal("shared.test.local", certificate);
        }

        [Fact]
        public async Task An_unknown_server_name_fails_the_handshake_unless_there_is_a_default()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var named = Certs.WritePfx(host.Directory, "named.test.local");
            var fallback = Certs.WritePfx(host.Directory, "fallback.test.local");

            host.Start(ProxyBuilder.Configuration("named", port, TrafficType.Https, host.Backend("NAMED").Port, c =>
                c.HostNames.Add(new NpHostName { Host = "named.test.local", TlsCertificatePath = named, TlsCertificatePassword = Certs.Password })));

            await Assert.ThrowsAnyAsync<Exception>(() => Client.Https(port, "other.test.local"));

            host.Start(ProxyBuilder.Configuration("default", port, TrafficType.Https, host.Backend("DEFAULT").Port, c =>
            {
                c.TlsCertificatePath = fallback;
                c.TlsCertificatePassword = Certs.Password;
            }));

            var (body, certificate) = await Client.Https(port, "other.test.local");
            Assert.Contains("backend=DEFAULT", body);
            Assert.Equal("fallback.test.local", certificate);
        }

        [Fact]
        public async Task A_wildcard_certificate_serves_the_names_below_the_domain()
        {
            using var host = new TestHost();
            var port = Net.FreePort();
            var wildcard = Certs.WritePfx(host.Directory, "wild.test.local");

            host.Start(ProxyBuilder.Configuration("wild", port, TrafficType.Https, host.Backend("WILD").Port, c =>
                c.HostNames.Add(new NpHostName { Host = "*.test.local", TlsCertificatePath = wildcard, TlsCertificatePassword = Certs.Password })));

            var (body, certificate) = await Client.Https(port, "anything.test.local");
            Assert.Contains("backend=WILD", body);
            Assert.Equal("wild.test.local", certificate);
        }

        [Fact]
        public void A_certificate_that_cannot_be_loaded_keeps_the_proxy_from_starting_with_the_reason()
        {
            using var host = new TestHost();
            var path = host.Directory.File("broken.pfx");
            File.WriteAllBytes(path, [1, 2, 3]);

            var proxy = host.Create(ProxyBuilder.Configuration("p", Net.FreePort(), TrafficType.Https, host.Backend("A").Port, c => c.TlsCertificatePath = path));

            Assert.False(proxy.Start(out var error));
            Assert.Contains("broken.pfx", error);
        }

        [Fact]
        public async Task An_https_proxy_without_a_certificate_passes_the_encrypted_traffic_through_as_before()
        {
            using var host = new TestHost();
            var port = Net.FreePort();

            //Whatever arrives is sent to the endpoint untouched, so a TLS server behind it is what the client talks to.
            var passthrough = host.Own(new TlsEchoBackend(Certs.PfxBytes("behind.test.local")));
            host.Start(ProxyBuilder.Configuration("legacy", port, TrafficType.Https, passthrough.Port, c => c.Endpoints.Collection[0].TrafficType = TrafficType.Raw));

            var (body, certificate) = await Client.Https(port, "behind.test.local");

            Assert.Equal("behind.test.local", certificate);
            Assert.Contains("hello from tls", body);
        }
    }
}
