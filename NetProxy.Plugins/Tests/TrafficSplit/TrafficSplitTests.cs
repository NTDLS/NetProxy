using NetProxy.PluginApi;
using NetProxy.Plugins.Tests.Support;
using NetProxy.Plugins.TrafficSplit;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class TrafficSplitTests : PluginTestBase
    {
        private static readonly NpEndpointInfo Stable = new(Guid.NewGuid(), "10.0.0.5", 8080, false);
        private static readonly NpEndpointInfo Canary = new(Guid.NewGuid(), "10.0.0.6", 8080, false);
        private static readonly NpEndpointInfo Spare = new(Guid.NewGuid(), "10.0.0.7", 8080, false);

        private (TrafficSplitPlugin Plugin, FakeHost Host) Start(params string[] rules)
        {
            var settings = new Dictionary<string, string>();
            for (var i = 0; i < rules.Length; i++)
            {
                settings[$"Rule{i + 1}"] = rules[i];
            }

            return Start(new TrafficSplitPlugin(), settings);
        }

        private static List<string> Order(TrafficSplitPlugin plugin, FakeConnection connection, params NpEndpointInfo[] endpoints)
        {
            var order = new NpEndpointOrderEventArgs(connection, endpoints);
            plugin.OnEndpointsOrderingAsync(order, CancellationToken.None).AsTask().Wait();
            return order.Endpoints.Select(o => o.Address).ToList();
        }

        private static FakeConnection Client(int n) => new() { ClientAddress = $"198.51.{n / 250}.{n % 250 + 1}" };

        [Fact]
        public void A_share_of_the_clients_goes_to_the_canary_first_and_the_rest_to_the_stable_one()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=10");

            var first = Enumerable.Range(0, 5000).Select(n => Order(plugin, Client(n), Canary, Stable)[0]).ToList();
            var canary = first.Count(o => o == "10.0.0.6");

            Assert.InRange(canary, 400, 600); //About 10% of 5000.
            Assert.Equal(5000 - canary, first.Count(o => o == "10.0.0.5"));
        }

        [Fact]
        public void The_same_client_always_lands_on_the_same_side()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=50");

            foreach (var n in Enumerable.Range(0, 200))
            {
                var first = Order(plugin, Client(n), Stable, Canary);
                Assert.All(Enumerable.Range(0, 5), _ => Assert.Equal(first, Order(plugin, Client(n), Stable, Canary)));
            }
        }

        [Fact]
        public void A_client_that_is_chosen_has_the_other_endpoints_behind_it_as_fallbacks()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=100");

            Assert.Equal(["10.0.0.6", "10.0.0.5", "10.0.0.7"], Order(plugin, Client(1), Stable, Spare, Canary).OrderBy(o => o == "10.0.0.6" ? 0 : 1).ThenBy(o => o).ToList());
            Assert.Equal("10.0.0.6", Order(plugin, Client(1), Stable, Spare, Canary)[0]);
            Assert.Equal(3, Order(plugin, Client(1), Stable, Spare, Canary).Count);
        }

        [Fact]
        public void Nought_percent_keeps_everyone_off_the_canary_and_a_hundred_puts_everyone_on_it()
        {
            var (none, _) = Start("endpoints=10.0.0.6:8080; percent=0");
            var (all, _) = Start("endpoints=10.0.0.6:8080; percent=100");

            Assert.All(Enumerable.Range(0, 300), n => Assert.Equal("10.0.0.5", Order(none, Client(n), Canary, Stable)[0]));
            Assert.All(Enumerable.Range(0, 300), n => Assert.Equal("10.0.0.6", Order(all, Client(n), Stable, Canary)[0]));
        }

        [Fact]
        public void Fractions_of_a_percent_work()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=0.5%");

            var canary = Enumerable.Range(0, 20_000).Count(n => Order(plugin, Client(n), Stable, Canary)[0] == "10.0.0.6");

            Assert.InRange(canary, 50, 160); //About 100.
        }

        [Fact]
        public void In_only_mode_nobody_has_a_fallback()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=50; mode=only");

            var results = Enumerable.Range(0, 400).Select(n => Order(plugin, Client(n), Stable, Canary)).ToList();

            Assert.All(results, o => Assert.Single(o));
            Assert.Contains(results, o => o[0] == "10.0.0.6");
            Assert.Contains(results, o => o[0] == "10.0.0.5");
        }

        [Fact]
        public void Choosing_per_connection_spreads_even_one_client_over_both_sides()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=50; sticky=connection");
            var client = Client(1);

            var first = Enumerable.Range(0, 400).Select(_ => Order(plugin, client, Stable, Canary)[0]).ToList();

            Assert.InRange(first.Count(o => o == "10.0.0.6"), 140, 260);
        }

        [Fact]
        public void A_rule_can_be_for_one_proxy_and_the_endpoints_are_matched_by_address_and_port()
        {
            var (plugin, _) = Start("proxy=api; endpoints=10.0.0.6:8080; percent=100");
            var api = new FakeConnection { Proxy = new(Guid.NewGuid(), "API", 443, NpTrafficKind.Https) };

            Assert.Equal("10.0.0.6", Order(plugin, api, Stable, Canary)[0]);
            Assert.Equal("10.0.0.5", Order(plugin, Client(1), Stable, Canary)[0]); //The proxy is called web.

            var otherPort = new NpEndpointInfo(Guid.NewGuid(), "10.0.0.6", 9090, false);
            Assert.Equal("10.0.0.5", Order(plugin, api, Stable, otherPort)[0]); //Not the endpoint of the rule.
        }

        [Fact]
        public void Rules_apply_one_after_the_other()
        {
            var (plugin, _) = Start("endpoints=10.0.0.6:8080; percent=100", "endpoints=10.0.0.7:8080; percent=100");

            Assert.Equal("10.0.0.7", Order(plugin, Client(1), Stable, Canary, Spare)[0]); //The last rule has the last word, the first is the fallback.
            Assert.Equal(["10.0.0.7", "10.0.0.6", "10.0.0.5"], Order(plugin, Client(1), Stable, Canary, Spare));
        }

        [Fact]
        public void A_rule_that_names_all_the_endpoints_or_none_of_them_changes_nothing()
        {
            var (plugin, _) = Start("endpoints=10.0.0.5:8080, 10.0.0.6:8080; percent=50", "endpoints=10.9.9.9:80; percent=50");

            Assert.Equal(["10.0.0.5", "10.0.0.6"], Order(plugin, Client(1), Stable, Canary));
        }

        [Theory]
        [InlineData("percent=10")]
        [InlineData("endpoints=10.0.0.6:8080")]
        [InlineData("endpoints=10.0.0.6; percent=10")]
        [InlineData("endpoints=10.0.0.6:99999; percent=10")]
        [InlineData("endpoints=10.0.0.6:8080; percent=101")]
        [InlineData("endpoints=10.0.0.6:8080; percent=x")]
        [InlineData("endpoints=10.0.0.6:8080; percent=10; sticky=sometimes")]
        [InlineData("endpoints=10.0.0.6:8080; percent=10; mode=all")]
        [InlineData("endpoints=10.0.0.6:8080; percent=10; weight=3")]
        [InlineData("endpoints=10.0.0.6:8080; percent")]
        public void Rules_that_are_wrong_are_reported_and_left_out(string rule)
        {
            var (plugin, host) = Start(rule);

            Assert.Contains(host.Texts, o => o.StartsWith("Rule1 is left out."));
            Assert.Contains(host.Texts, o => o.StartsWith("There are no rules"));
            Assert.Equal(["10.0.0.5", "10.0.0.6"], Order(plugin, Client(1), Stable, Canary));
        }

        [Fact]
        public void The_split_is_reported_each_minute()
        {
            var (plugin, host) = Start("endpoints=10.0.0.6:8080; percent=50");
            foreach (var n in Enumerable.Range(0, 100)) { Order(plugin, Client(n), Stable, Canary); }

            plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();

            Assert.Contains(host.Texts, o => o.StartsWith("In the last minute: rule 1 listed:") && o.Contains("rule 1 others:"));
            Assert.Contains(host.Texts, o => o.StartsWith("50% of clients go first to 10.0.0.6:8080"));
        }
    }
}
