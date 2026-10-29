using NetProxy.PluginApi;
using NetProxy.Plugins.GeoFilter;
using NetProxy.Plugins.Tests.Support;
using System.Net;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class GeoFilterTests : PluginTestBase
    {
        private sealed class FakeLookup : IGeoLookup
        {
            public Dictionary<string, string> Countries { get; } = new()
            {
                ["203.0.113.7"] = "US", ["198.51.100.9"] = "CN", ["192.0.2.50"] = "RU", ["100.64.0.1"] = "DE"
            };

            public Dictionary<string, long> Asns { get; } = new() { ["203.0.113.7"] = 64500, ["198.51.100.9"] = 64501 };

            public string? Country(IPAddress address) => Countries.GetValueOrDefault(address.ToString());

            public long? Asn(IPAddress address) => Asns.TryGetValue(address.ToString(), out var asn) ? asn : null;
        }

        private readonly FakeLookup _lookup = new();

        private (GeoFilterPlugin Plugin, FakeHost Host) Start(Dictionary<string, string> settings, bool withLookup = true)
            => Start(withLookup ? new GeoFilterPlugin(_lookup) : new GeoFilterPlugin(), settings);

        private static FakeConnection From(string address, bool http = true)
            => new() { ClientAddress = address, Proxy = new(Guid.NewGuid(), "web", 443, http ? NpTrafficKind.Https : NpTrafficKind.Tcp) };

        [Fact]
        public void With_no_rules_everyone_is_let_in()
        {
            var (plugin, host) = Start(new());

            Assert.True(Connect(plugin, From("198.51.100.9")).Allowed);
            Assert.Contains(host.Texts, o => o.StartsWith("There are no rules"));
        }

        [Fact]
        public void A_country_that_is_denied_is_turned_away_and_the_others_are_not()
        {
            var (plugin, host) = Start(new() { ["DenyCountries"] = "cn, ru" });

            Assert.False(Connect(plugin, From("198.51.100.9")).Allowed);
            Assert.False(Connect(plugin, From("192.0.2.50")).Allowed);
            Assert.True(Connect(plugin, From("203.0.113.7")).Allowed);
            Assert.Contains(host.Texts, o => o.StartsWith("Turning away 198.51.100.9 (country CN)"));
        }

        [Fact]
        public void With_a_list_of_countries_that_are_allowed_only_they_are()
        {
            var (plugin, _) = Start(new() { ["AllowCountries"] = "US, DE" });

            Assert.True(Connect(plugin, From("203.0.113.7")).Allowed);
            Assert.True(Connect(plugin, From("100.64.0.1")).Allowed);
            Assert.False(Connect(plugin, From("198.51.100.9")).Allowed);
            Assert.Contains("country CN", Connect(plugin, From("198.51.100.9")).Reason);
        }

        [Fact]
        public void An_address_that_can_not_be_placed_is_let_in_unless_that_is_switched_off()
        {
            var (open, _) = Start(new() { ["AllowCountries"] = "US" });
            Assert.True(Connect(open, From("203.0.114.1")).Allowed);

            var (strict, _) = Start(new() { ["AllowCountries"] = "US", ["AllowUnknown"] = "false" });
            Assert.False(Connect(strict, From("203.0.114.1")).Allowed);
            Assert.Contains("unknown country", Connect(strict, From("203.0.114.1")).Reason);
        }

        [Fact]
        public void A_network_can_be_denied_and_a_network_can_be_let_in_whatever_its_country()
        {
            var (plugin, _) = Start(new() { ["DenyCountries"] = "US", ["DenyAsns"] = "AS64501", ["AllowAsns"] = "64500" });

            Assert.True(Connect(plugin, From("203.0.113.7")).Allowed); //US, but its network is let in.
            Assert.False(Connect(plugin, From("198.51.100.9")).Allowed);
            Assert.Contains("AS64501", Connect(plugin, From("198.51.100.9")).Reason);
        }

        [Fact]
        public void Private_addresses_and_the_allow_list_are_never_turned_away()
        {
            _lookup.Countries["10.1.2.3"] = "CN";
            _lookup.Countries["198.51.100.20"] = "CN";
            var (plugin, _) = Start(new() { ["DenyCountries"] = "CN", ["Allow"] = "198.51.100.20" });

            Assert.True(Connect(plugin, From("10.1.2.3")).Allowed);
            Assert.True(Connect(plugin, From("198.51.100.20")).Allowed);
            Assert.False(Connect(plugin, From("198.51.100.9")).Allowed);
        }

        [Fact]
        public void In_log_only_nobody_is_turned_away_and_the_log_says_who_would_have_been()
        {
            var (plugin, host) = Start(new() { ["DenyCountries"] = "CN", ["LogOnly"] = "true" });

            Assert.True(Connect(plugin, From("198.51.100.9")).Allowed);
            Assert.Contains(host.Texts, o => o.StartsWith("Would turn away 198.51.100.9"));

            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            Assert.Contains(host.Texts, o => o.StartsWith("Would have turned away 1 connection(s)"));
        }

        [Fact]
        public void A_summary_of_who_was_turned_away_is_logged_each_minute()
        {
            var (plugin, host) = Start(new() { ["DenyCountries"] = "CN, RU" });

            for (var i = 0; i < 5; i++) { Connect(plugin, From("198.51.100.9")); }
            Connect(plugin, From("192.0.2.50"));
            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();

            Assert.Contains(host.Texts, o => o.StartsWith("Turned away 6 connection(s)") && o.Contains("country CN (5)") && o.Contains("country RU (1)"));
        }

        //---------------------------------------------------------------- Behind a proxy

        private static byte[] Through(string client, string country = "CN")
            => Http.Request("/", extraHeaders: $"CF-Connecting-IP: {client}\r\nCF-IPCountry: {country}\r\n");

        [Fact]
        public void Behind_cloudflare_the_client_is_placed_by_the_database_and_cloudflare_is_never_turned_away()
        {
            var (plugin, _) = Start(new() { ["DenyCountries"] = "CN", ["TrustedProxies"] = "cloudflare" });
            var edge = From("104.16.1.1");

            Assert.True(Connect(plugin, edge).Allowed);

            var denied = Send(plugin, edge, Through("198.51.100.9", "US")); //The database knows better than the header.
            Assert.True(denied.IsDropped);
            Assert.StartsWith("HTTP/1.1 403 Forbidden", Http.Text(denied.Response));
            Assert.False(denied.CloseAfterResponse);

            Assert.False(Send(plugin, edge, Through("203.0.113.7", "CN")).IsDropped);
        }

        [Fact]
        public void Without_a_database_the_country_header_of_cloudflare_is_used_but_only_from_cloudflare()
        {
            var (plugin, host) = Start(new() { ["DenyCountries"] = "CN", ["TrustedProxies"] = "cloudflare" }, withLookup: false);

            Assert.True(Send(plugin, From("104.16.1.1"), Through("198.51.100.9", "CN")).IsDropped);
            Assert.False(Send(plugin, From("104.16.1.1"), Through("203.0.113.7", "US")).IsDropped);
            Assert.False(Send(plugin, From("104.16.1.1"), Through("203.0.113.8", "XX")).IsDropped); //Cloudflare does not know.
            Assert.False(Send(plugin, From("203.0.113.50"), Through("203.0.113.8", "CN")).IsDropped); //Not Cloudflare: the header is not believed.
            Assert.DoesNotContain(host.Texts, o => o.Contains("nobody can be placed"));
        }

        [Fact]
        public void A_head_that_arrives_in_pieces_is_held_back_until_it_can_be_judged()
        {
            var (plugin, _) = Start(new() { ["DenyCountries"] = "CN", ["TrustedProxies"] = "cloudflare" });
            var edge = From("104.16.1.1");
            var bytes = Through("198.51.100.9");

            var first = Send(plugin, edge, bytes.AsSpan(0, 20).ToArray());
            Assert.True(first.IsDropped);
            Assert.Null(first.Response);

            var second = Send(plugin, edge, bytes.AsSpan(20).ToArray());
            Assert.StartsWith("HTTP/1.1 403", Http.Text(second.Response));
        }

        [Fact]
        public void A_client_that_can_not_be_answered_has_its_connection_closed()
        {
            var (plugin, _) = Start(new() { ["DenyCountries"] = "CN", ["TrustedProxies"] = "cloudflare" });

            var data = Send(plugin, From("104.16.1.1"), Through("198.51.100.9"), canRespond: false);

            Assert.True(data.IsDropped);
            Assert.NotNull(data.CloseReason);
        }

        //---------------------------------------------------------------- Settings and databases

        [Fact]
        public void Settings_that_are_wrong_are_reported()
        {
            var (_, host) = Start(new() { ["DenyCountries"] = "USA, DE", ["DenyAsns"] = "x", ["Allow"] = "nonsense" }, withLookup: false);

            Assert.Contains(host.Texts, o => o.Contains("'USA'") && o.Contains("country code"));
            Assert.Contains(host.Texts, o => o.Contains("'x'") && o.Contains("AS number"));
            Assert.Contains(host.Texts, o => o.Contains("'nonsense'"));
            Assert.Contains(host.Texts, o => o.Contains("nobody can be placed"));
        }

        [Fact]
        public void A_database_that_is_missing_or_broken_is_reported_and_does_not_stop_the_plugin()
        {
            var directory = TempDirectory();
            File.WriteAllText(Path.Combine(directory, "broken.mmdb"), "this is not a database");

            var (missing, host1) = Start(new GeoFilterPlugin(), new() { ["CountryDatabase"] = "nothing.mmdb", ["DenyCountries"] = "CN" }, directory);
            Assert.Contains(host1.Texts, o => o.Contains("nothing.mmdb") && o.Contains("does not exist yet"));
            Assert.True(Connect(missing, From("198.51.100.9")).Allowed);

            var (broken, host2) = Start(new GeoFilterPlugin(), new() { ["CountryDatabase"] = "broken.mmdb", ["DenyCountries"] = "CN" }, directory);
            Assert.Contains(host2.Messages, o => o.Level == NpPluginLogLevel.Warning && o.Message.Contains("could not be read"));
            Assert.True(Connect(broken, From("198.51.100.9")).Allowed);
        }
    }
}
