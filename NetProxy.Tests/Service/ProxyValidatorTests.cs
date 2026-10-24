using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service;
using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Service
{
    public class ProxyValidatorTests
    {
        private static NpProxyConfiguration Good(string name = "web", int port = 8080)
            => ProxyBuilder.Configuration(name, port, TrafficType.Http, 9000);

        [Fact]
        public void A_good_proxy_has_no_problems()
            => Assert.Empty(NpProxyValidator.Validate(Good(), []));

        [Fact]
        public void The_proxy_needs_a_name()
        {
            var proxy = Good(" ");
            Assert.Contains(NpProxyValidator.Validate(proxy, []), o => o.Contains("needs a name"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void The_listen_port_has_to_be_a_port(int port)
            => Assert.Contains(NpProxyValidator.Validate(Good(port: port), []), o => o.Contains("listen port"));

        [Fact]
        public void The_proxy_needs_an_endpoint()
        {
            var proxy = Good();
            proxy.Endpoints.Collection.Clear();

            Assert.Contains(NpProxyValidator.Validate(proxy, []), o => o.Contains("endpoint"));
        }

        [Fact]
        public void An_endpoint_needs_an_address_and_a_port()
        {
            var proxy = Good();
            proxy.Endpoints.Collection[0].Port = 70000;
            Assert.NotEmpty(NpProxyValidator.Validate(proxy, []));

            proxy.Endpoints.Collection[0].Port = 80;
            proxy.Endpoints.Collection[0].Address = " ";
            Assert.NotEmpty(NpProxyValidator.Validate(proxy, []));
        }

        [Fact]
        public void A_binding_has_to_be_an_ip_address_unless_the_proxy_listens_on_all_addresses()
        {
            var proxy = Good();
            proxy.Bindings[0].Address = "not-an-address";
            Assert.Contains(NpProxyValidator.Validate(proxy, []), o => o.Contains("not a valid"));

            proxy.ListenOnAllAddresses = true;
            Assert.Empty(NpProxyValidator.Validate(proxy, []));
        }

        [Fact]
        public void An_ipv6_address_in_brackets_is_a_valid_binding()
        {
            var proxy = Good();
            proxy.Bindings[0].Address = "[::1]";
            Assert.Empty(NpProxyValidator.Validate(proxy, []));
        }

        [Fact]
        public void Without_listening_on_all_addresses_a_binding_is_needed()
        {
            var proxy = Good();
            proxy.Bindings.Clear();
            Assert.Contains(NpProxyValidator.Validate(proxy, []), o => o.Contains("binding"));
        }

        [Fact]
        public void Names_are_unique_whatever_the_case()
        {
            var other = Good("Web", 9090);
            Assert.Contains(NpProxyValidator.Validate(Good("web"), [other]), o => o.Contains("already a proxy named"));
        }

        [Fact]
        public void A_proxy_may_keep_its_own_name()
        {
            var proxy = Good("web");
            Assert.Empty(NpProxyValidator.Validate(proxy, [proxy]));
        }

        [Fact]
        public void Sharing_rules_are_part_of_the_validation()
        {
            var other = Good("a", 443);
            var proxy = Good("b", 443);
            Assert.Contains(NpProxyValidator.Validate(proxy, [other]), o => o.Contains("443"));
        }

        [Fact]
        public void Host_names_are_cleaned_up_as_part_of_the_validation()
        {
            var proxy = Good();
            proxy.HostNames.Add(new NpHostName { Host = " WWW.Example.COM " });

            Assert.Empty(NpProxyValidator.Validate(proxy, []));
            Assert.Equal("www.example.com", proxy.HostNames[0].Host);
        }

        [Fact]
        public void An_https_proxy_without_a_certificate_is_not_refused_because_older_files_have_them()
        {
            var proxy = ProxyBuilder.Configuration("legacy", 8443, TrafficType.Https, 9000);
            Assert.Empty(NpProxyValidator.Validate(proxy, []));
        }
    }
}
