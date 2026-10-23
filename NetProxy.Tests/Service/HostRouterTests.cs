using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Proxy;
using NetProxy.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class HostRouterTests
    {
        private static NpProxy Proxy(string name, TrafficType type = TrafficType.Http, params string[] hosts)
        {
            var configuration = ProxyBuilder.Configuration(name, 443, type, 9999);
            foreach (var host in hosts)
            {
                configuration.HostNames.Add(new NpHostName { Host = host });
            }

            return new NpProxy(configuration);
        }

        [Fact]
        public void Match_prefers_an_exact_name_to_a_wildcard()
        {
            var wildcard = Proxy("wildcard", hosts: "*.example.com");
            var exact = Proxy("exact", hosts: "www.example.com");

            Assert.Same(exact, NpHostRouter.Match([wildcard, exact], "www.example.com"));
            Assert.Same(wildcard, NpHostRouter.Match([wildcard, exact], "shop.example.com"));
        }

        [Fact]
        public void Match_prefers_the_longer_wildcard()
        {
            var broad = Proxy("broad", hosts: "*.example.com");
            var narrow = Proxy("narrow", hosts: "*.eu.example.com");

            Assert.Same(narrow, NpHostRouter.Match([broad, narrow], "shop.eu.example.com"));
            Assert.Same(broad, NpHostRouter.Match([broad, narrow], "shop.us.example.com"));
        }

        [Fact]
        public void Match_ignores_case_a_port_and_a_trailing_dot()
        {
            var proxy = Proxy("a", hosts: "www.example.com");

            Assert.Same(proxy, NpHostRouter.Match([proxy], "WWW.Example.COM:8443"));
            Assert.Same(proxy, NpHostRouter.Match([proxy], "www.example.com."));
        }

        [Fact]
        public void Match_falls_back_to_the_proxy_without_host_names()
        {
            var named = Proxy("named", hosts: "www.example.com");
            var fallback = Proxy("default");

            Assert.Same(fallback, NpHostRouter.Match([named, fallback], "other.example.net"));
            Assert.Same(fallback, NpHostRouter.Match([named, fallback], null));
            Assert.Same(fallback, NpHostRouter.Match([named, fallback], ""));
        }

        [Fact]
        public void Match_gives_nothing_when_no_name_matches_and_there_is_no_default()
        {
            var named = Proxy("named", hosts: "www.example.com");

            Assert.Null(NpHostRouter.Match([named], "other.example.net"));
            Assert.Null(NpHostRouter.Match([named], null));
        }

        [Fact]
        public void A_disabled_host_name_is_not_matched()
        {
            var configuration = ProxyBuilder.Configuration("p", 443, TrafficType.Http, 9999);
            configuration.HostNames.Add(new NpHostName { Host = "off.example.com", Enabled = false });
            var proxy = new NpProxy(configuration);

            Assert.Null(NpHostRouter.Match([proxy], "off.example.com"));
            Assert.Empty(proxy.HostPatterns);
        }

        [Fact]
        public void A_proxy_whose_host_names_are_all_switched_off_does_not_become_the_default()
        {
            var configuration = ProxyBuilder.Configuration("p", 443, TrafficType.Http, 9999);
            configuration.HostNames.Add(new NpHostName { Host = "off.example.com", Enabled = false });
            var switchedOff = new NpProxy(configuration);
            var named = Proxy("named", hosts: "www.example.com");

            Assert.False(switchedOff.IsDefaultForPort);
            Assert.Null(NpHostRouter.Match([switchedOff, named], "unknown.example.com"));

            //And it does not count as a second default.
            Assert.Null(NpHostRouter.FindSharingConflict(configuration, [Config("default", TrafficType.Http, 443)]));
        }

        //---------------------------------------------------------------- Reading the Host header

        [Fact]
        public void TryReadHost_waits_for_the_end_of_the_header()
        {
            var partial = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: www.example.com\r\n");

            Assert.False(NpHostRouter.TryReadHost(partial, out var host));
            Assert.Null(host);
        }

        [Theory]
        [InlineData("GET / HTTP/1.1\r\nHost: www.example.com\r\n\r\n", "www.example.com")]
        [InlineData("GET / HTTP/1.1\r\nhost:   WWW.Example.com:8080  \r\nAccept: */*\r\n\r\n", "www.example.com")]
        [InlineData("POST /a HTTP/1.1\r\nAccept: */*\r\nHOST: a.b\r\nContent-Length: 0\r\n\r\n", "a.b")]
        [InlineData("GET / HTTP/1.1\r\nHost: [::1]:443\r\n\r\n", "[::1]")]
        public void TryReadHost_finds_the_host(string request, string expected)
        {
            Assert.True(NpHostRouter.TryReadHost(Encoding.ASCII.GetBytes(request), out var host));
            Assert.Equal(expected, host);
        }

        [Fact]
        public void TryReadHost_says_there_is_none_when_the_header_has_no_host()
        {
            Assert.True(NpHostRouter.TryReadHost(Encoding.ASCII.GetBytes("GET / HTTP/1.0\r\nAccept: */*\r\n\r\n"), out var host));
            Assert.Null(host);
        }

        [Fact]
        public void TryReadHost_does_not_take_the_request_line_or_other_headers_for_the_host()
        {
            Assert.True(NpHostRouter.TryReadHost(Encoding.ASCII.GetBytes("GET /host: evil HTTP/1.1\r\nX-Host: evil\r\nHost: good\r\n\r\n"), out var host));
            Assert.Equal("good", host);
        }

        //---------------------------------------------------------------- Sharing a port

        private static NpProxyConfiguration Config(string name, TrafficType type, int port, params string[] hosts)
        {
            var configuration = ProxyBuilder.Configuration(name, port, type, 9999);
            foreach (var host in hosts)
            {
                configuration.HostNames.Add(new NpHostName { Host = host });
            }

            return configuration;
        }

        [Fact]
        public void Proxies_with_different_hosts_can_share_a_port()
            => Assert.Null(NpHostRouter.FindSharingConflict(
                Config("b", TrafficType.Https, 443, "b.example.com"), [Config("a", TrafficType.Https, 443, "a.example.com")]));

        [Fact]
        public void Proxies_on_different_ports_never_conflict()
            => Assert.Null(NpHostRouter.FindSharingConflict(
                Config("b", TrafficType.Raw, 444), [Config("a", TrafficType.Raw, 443)]));

        [Fact]
        public void A_raw_proxy_cannot_share_a_port()
        {
            Assert.NotNull(NpHostRouter.FindSharingConflict(Config("b", TrafficType.Raw, 443), [Config("a", TrafficType.Http, 443, "a.example.com")]));
            Assert.NotNull(NpHostRouter.FindSharingConflict(Config("b", TrafficType.Http, 443, "b.example.com"), [Config("a", TrafficType.Raw, 443)]));
        }

        [Fact]
        public void Http_and_https_cannot_share_a_port()
            => Assert.NotNull(NpHostRouter.FindSharingConflict(
                Config("b", TrafficType.Https, 443, "b.example.com"), [Config("a", TrafficType.Http, 443, "a.example.com")]));

        [Fact]
        public void Only_one_proxy_per_port_can_be_the_default()
            => Assert.NotNull(NpHostRouter.FindSharingConflict(Config("b", TrafficType.Http, 443), [Config("a", TrafficType.Http, 443)]));

        [Fact]
        public void The_same_host_name_cannot_be_used_twice_on_a_port_even_in_another_form()
            => Assert.NotNull(NpHostRouter.FindSharingConflict(
                Config("b", TrafficType.Http, 443, "A.Example.com"), [Config("a", TrafficType.Http, 443, "a.example.com")]));

        [Fact]
        public void A_proxy_does_not_conflict_with_itself()
        {
            var proxy = Config("a", TrafficType.Http, 443);
            Assert.Null(NpHostRouter.FindSharingConflict(proxy, [proxy]));
        }

        [Fact]
        public void Proxies_on_different_addresses_of_the_same_port_do_not_conflict()
        {
            var first = Config("a", TrafficType.Raw, 443);
            var second = Config("b", TrafficType.Raw, 443);
            second.Bindings[0].Address = "127.0.0.2";

            Assert.Null(NpHostRouter.FindSharingConflict(second, [first]));
        }

        [Fact]
        public void Listening_on_all_addresses_overlaps_every_binding()
        {
            var specific = Config("a", TrafficType.Raw, 443);
            var all = Config("b", TrafficType.Raw, 443);
            all.ListenOnAllAddresses = true;

            Assert.NotNull(NpHostRouter.FindSharingConflict(all, [specific]));
        }

        //---------------------------------------------------------------- Cleaning up host names

        [Fact]
        public void NormalizeAndValidate_cleans_up_the_names_and_drops_blanks()
        {
            var configuration = Config("a", TrafficType.Http, 443, " WWW.Example.com ", "", "  ");

            Assert.Null(NpHostRouter.NormalizeAndValidate(configuration));
            Assert.Equal(["www.example.com"], configuration.HostNames.Select(o => o.Host));
        }

        [Fact]
        public void NormalizeAndValidate_refuses_an_invalid_name()
            => Assert.NotNull(NpHostRouter.NormalizeAndValidate(Config("a", TrafficType.Http, 443, "not a host")));

        [Fact]
        public void NormalizeAndValidate_refuses_the_same_name_twice()
            => Assert.NotNull(NpHostRouter.NormalizeAndValidate(Config("a", TrafficType.Http, 443, "a.example.com", "A.EXAMPLE.COM")));
    }
}
