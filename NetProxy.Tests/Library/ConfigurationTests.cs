using Newtonsoft.Json;
using NetProxy.Library;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using NetProxy.Tests.Support;
using Xunit;

namespace NetProxy.Tests.Library
{
    public class ConfigurationTests
    {
        [Fact]
        public void A_new_proxy_has_its_own_id_and_sensible_defaults()
        {
            var first = new NpProxyConfiguration();
            var second = new NpProxyConfiguration();

            Assert.NotEqual(Guid.Empty, first.Id);
            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal(Constants.DefaultInitialBufferSize, first.InitialBufferSize);
            Assert.Equal(Constants.DefaultMaxBufferSize, first.MaxBufferSize);
            Assert.Equal(Constants.DefaultAcceptBacklogSize, first.AcceptBacklogSize);
            Assert.Equal(ForwardedHeadersMode.Off, first.ForwardedHeaders);
            Assert.Empty(first.PluginIds);
            Assert.Empty(first.HostNames);
        }

        [Fact]
        public void Clone_is_a_deep_copy()
        {
            var original = ProxyBuilder.Configuration("a", 80, TrafficType.Https, 9000, c =>
            {
                c.HostNames.Add(new NpHostName { Host = "a.example.com", TlsCertificatePassword = "secret" });
                c.PluginIds.Add(Guid.NewGuid());
                c.ForwardedHeaders = ForwardedHeadersMode.Append;
            });

            var copy = original.Clone();
            copy.HostNames[0].Host = "changed";
            copy.Endpoints.Collection[0].Port = 1;
            copy.PluginIds.Clear();

            Assert.Equal("a.example.com", original.HostNames[0].Host);
            Assert.Equal(9000, original.Endpoints.Collection[0].Port);
            Assert.Single(original.PluginIds);
            Assert.Equal(original.Id, original.Clone().Id);
            Assert.Equal(ForwardedHeadersMode.Append, original.Clone().ForwardedHeaders);
            Assert.Equal("secret", original.Clone().HostNames[0].TlsCertificatePassword);
        }

        [Fact]
        public void Enums_are_written_as_names()
        {
            var json = JsonConvert.SerializeObject(ProxyBuilder.Configuration("a", 80, TrafficType.Https, 9000, c => c.ForwardedHeaders = ForwardedHeadersMode.Replace));

            Assert.Contains("\"TrafficType\":\"Https\"", json);
            Assert.Contains("\"ForwardedHeaders\":\"Replace\"", json);
        }

        [Fact]
        public void The_traffic_types_are_tcp_http_https_and_udp()
        {
            Assert.Equal(["Tcp", "Http", "Https", "Udp"], Enum.GetNames<TrafficType>());
            Assert.Equal(["Tcp", "Http", "Https", "Udp"], Enum.GetNames<NetProxy.PluginApi.NpTrafficKind>());
            Assert.Contains("\"TrafficType\":\"Tcp\"", JsonConvert.SerializeObject(ProxyBuilder.Configuration("a", 80, TrafficType.Tcp, 9000)));
            Assert.Equal(TrafficType.Tcp, default(TrafficType));
        }

        [Fact]
        public void Files_from_before_a_setting_existed_still_load_with_its_default()
        {
            var loaded = JsonConvert.DeserializeObject<NpProxyConfiguration>(
                "{\"Name\":\"old\",\"ListenPort\":80,\"TrafficType\":\"Http\",\"Bindings\":[],\"Endpoints\":{\"Collection\":[]}}")!;

            Assert.Equal("old", loaded.Name);
            Assert.Empty(loaded.HostNames);
            Assert.Empty(loaded.PluginIds);
            Assert.Equal(ForwardedHeadersMode.Off, loaded.ForwardedHeaders);
            Assert.Equal(Constants.DefaultMaxBufferSize, loaded.MaxBufferSize);
        }

        [Theory]
        [InlineData("IPv4", BindingProtocol.IPv4)]
        [InlineData("IPv6", BindingProtocol.IPv6)]
        [InlineData("Both", BindingProtocol.Both)]
        public void The_binding_protocol_is_read_by_name(string name, BindingProtocol expected)
        {
            var loaded = JsonConvert.DeserializeObject<NpProxyConfiguration>($"{{\"BindingProtocol\":\"{name}\"}}")!;
            Assert.Equal(expected, loaded.BindingProtocol);
        }

        [Fact]
        public void A_proxy_survives_a_trip_through_json()
        {
            var original = ProxyBuilder.Configuration("a", 80, TrafficType.Https, 9000, c =>
            {
                c.HostNames.Add(new NpHostName { Host = "a.example.com", TlsCertificatePath = "a.pfx", Enabled = false });
                c.HttpHeaderRules.Add(new NpHttpHeaderRule { Name = "X-A", Value = "1", Action = HttpHeaderAction.Upsert, HeaderType = HttpHeaderType.Request });
                c.UseStickySessions = true;
                c.AllowHttp2 = true;
            });

            var copy = JsonConvert.DeserializeObject<NpProxyConfiguration>(JsonConvert.SerializeObject(original))!;

            Assert.Equal(JsonConvert.SerializeObject(original), JsonConvert.SerializeObject(copy));
        }

        //---------------------------------------------------------------- Switched on unless said otherwise

        [Fact]
        public void A_new_endpoint_and_a_new_binding_are_switched_on()
        {
            Assert.True(new NpEndpoint().Enabled);
            Assert.True(new NpBinding().Enabled);
        }

        [Fact]
        public void An_endpoint_or_binding_in_a_file_without_enabled_is_switched_on_and_an_explicit_false_stays_false()
        {
            var loaded = JsonConvert.DeserializeObject<NpProxyConfiguration>("""
                {
                  "Name": "by-hand", "ListenPort": 80, "TrafficType": "Http",
                  "Bindings": [ { "Address": "127.0.0.1" }, { "Address": "127.0.0.2", "Enabled": false } ],
                  "Endpoints": { "Collection": [ { "Address": "10.0.0.1", "Port": 80 }, { "Address": "10.0.0.2", "Port": 80, "Enabled": false } ] }
                }
                """)!;

            Assert.Equal([true, false], loaded.Bindings.Select(o => o.Enabled));
            Assert.Equal([true, false], loaded.Endpoints.Collection.Select(o => o.Enabled));
        }

        //---------------------------------------------------------------- Endpoint ids

        [Fact]
        public void AdoptEndpointIds_keeps_the_ids_of_endpoints_that_did_not_change()
        {
            var existing = ProxyBuilder.Configuration("a", 80, TrafficType.Http, 9000);
            existing.Endpoints.Add(new NpEndpoint { Address = "10.0.0.2", Port = 9001 });

            var written = ProxyBuilder.Configuration("a", 80, TrafficType.Http, 9000);
            written.Endpoints.Add(new NpEndpoint { Address = "10.0.0.2", Port = 9001 });
            written.Endpoints.Add(new NpEndpoint { Address = "10.0.0.3", Port = 9002 });
            var brandNew = written.Endpoints.Collection[2].Id;

            written.AdoptEndpointIds(existing);

            Assert.Equal(existing.Endpoints.Collection[0].Id, written.Endpoints.Collection[0].Id);
            Assert.Equal(existing.Endpoints.Collection[1].Id, written.Endpoints.Collection[1].Id);
            Assert.Equal(brandNew, written.Endpoints.Collection[2].Id);
        }

        [Fact]
        public void AdoptEndpointIds_matches_each_existing_endpoint_only_once()
        {
            var existing = ProxyBuilder.Configuration("a", 80, TrafficType.Http, 9000);
            var written = ProxyBuilder.Configuration("a", 80, TrafficType.Http, 9000);
            written.Endpoints.Add(new NpEndpoint { Address = "127.0.0.1", Port = 9000 });

            written.AdoptEndpointIds(existing);

            Assert.NotEqual(written.Endpoints.Collection[0].Id, written.Endpoints.Collection[1].Id);
        }

        //---------------------------------------------------------------- Utilities

        [Fact]
        public void Sha256_is_the_lower_or_upper_case_hex_of_the_hash_and_stable()
        {
            var hash = NpUtility.Sha256("abc");

            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash.ToUpperInvariant());
            Assert.Equal(hash, NpUtility.Sha256("abc"));
            Assert.NotEqual(hash, NpUtility.Sha256("abd"));
        }

        [Theory]
        [InlineData("host", 80, "host:80")]
        [InlineData("10.0.0.1", 443, "10.0.0.1:443")]
        [InlineData("::1", 443, "[::1]:443")]
        [InlineData("[::1]", 443, "[::1]:443")]
        public void FormatHostPort_brackets_ipv6(string host, int port, string expected)
            => Assert.Equal(expected, NpUtility.FormatHostPort(host, port));

        [Theory]
        [InlineData("[::1]", "::1")]
        [InlineData("host", "host")]
        public void NormalizeHost_removes_brackets(string input, string expected)
            => Assert.Equal(expected, NpUtility.NormalizeHost(input));

        [Fact]
        public void The_plugin_configuration_has_the_default_time_limit()
            => Assert.Equal(Constants.DefaultPluginTimeoutMilliseconds, new NpPluginConfiguration().TimeoutMilliseconds);

        [Fact]
        public void A_user_list_adds_users()
        {
            var users = new NpUsers();
            users.Add(new NpUser { UserName = "a" });
            Assert.Single(users.Collection);
        }
    }
}
