using NetProxy.PluginApi;
using NetProxy.Plugins.Alerts;
using NetProxy.Plugins.Tests.Support;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class AlertsTests : PluginTestBase
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public List<(string Url, string Body, string? Authorization)> Requests { get; } = [];
            public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                lock (Requests)
                {
                    Requests.Add((request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString() ?? (request.Headers.TryGetValues("Authorization", out var v) ? v.First() : null)));
                }

                return new HttpResponseMessage(Status);
            }
        }

        private readonly CapturingHandler _handler = new();
        private readonly List<MailMessage> _mail = [];

        private (AlertsPlugin Plugin, FakeHost Host) Start(Dictionary<string, string> settings)
            => Start(new AlertsPlugin(_handler, Clock, (mail, _) =>
            {
                lock (_mail)
                {
                    _mail.Add(new MailMessage(mail.From!.Address, mail.To[0].Address, mail.Subject, mail.Body));
                }

                return Task.CompletedTask;
            }), settings);

        private static async Task<T> Eventually<T>(Func<T> read, Func<T, bool> done)
        {
            for (var i = 0; i < 100; i++)
            {
                var value = read();
                if (done(value))
                {
                    return value;
                }

                await Task.Delay(20);
            }

            return read();
        }

        private async Task<List<(string Url, string Body, string? Authorization)>> Sent(int count)
            => await Eventually(() => { lock (_handler.Requests) { return _handler.Requests.ToList(); } }, o => o.Count >= count);

        private static NpEndpointInfo Endpoint(int port = 8080, Guid? id = null) => new(id ?? Guid.NewGuid(), "10.0.0.5", port, false);

        private static void Fail(AlertsPlugin plugin, FakeConnection connection, NpEndpointInfo endpoint, int times)
        {
            for (var i = 0; i < times; i++)
            {
                plugin.OnEndpointConnectFailedAsync(connection, endpoint, "refused", CancellationToken.None).AsTask().Wait();
            }
        }

        [Fact]
        public async Task An_endpoint_that_fails_three_times_in_a_row_is_reported_once_to_discord()
        {
            var (plugin, _) = Start(new() { ["Discord.Url"] = "https://discord.example/api/webhooks/1/x" });
            var connection = new FakeConnection();
            var endpoint = Endpoint();

            Fail(plugin, connection, endpoint, 2);
            await Task.Delay(100);
            Assert.Empty(_handler.Requests);

            Fail(plugin, connection, endpoint, 5);
            var sent = await Sent(1);
            await Task.Delay(100);

            var request = Assert.Single(_handler.Requests);
            Assert.Equal("https://discord.example/api/webhooks/1/x", request.Url);
            var content = JsonDocument.Parse(request.Body).RootElement.GetProperty("content").GetString()!;
            Assert.Contains("Endpoint down: 10.0.0.5:8080", content);
            Assert.Contains("3 time(s) in a row", content);
            Assert.Contains("refused", content);
        }

        [Fact]
        public async Task When_the_endpoint_is_back_it_says_so_with_the_downtime()
        {
            var (plugin, _) = Start(new() { ["Slack.Url"] = "https://hooks.example/slack" });
            var connection = new FakeConnection();
            var endpoint = Endpoint();

            Fail(plugin, connection, endpoint, 3);
            await Sent(1);

            Clock.Advance(TimeSpan.FromMinutes(12));
            plugin.OnEndpointConnectedAsync(connection, endpoint, CancellationToken.None).AsTask().Wait();
            var sent = await Sent(2);

            var text = JsonDocument.Parse(sent[1].Body).RootElement.GetProperty("text").GetString()!;
            Assert.Contains("Endpoint back: 10.0.0.5:8080", text);
            Assert.Contains("12 minute(s)", text);

            //Connecting again is not news, and a failure after that starts the count again.
            plugin.OnEndpointConnectedAsync(connection, endpoint, CancellationToken.None).AsTask().Wait();
            await Task.Delay(100);
            Assert.Equal(2, _handler.Requests.Count);
        }

        [Fact]
        public async Task A_few_failures_between_successes_are_not_an_outage()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h" });
            var connection = new FakeConnection();
            var endpoint = Endpoint();

            for (var i = 0; i < 5; i++)
            {
                Fail(plugin, connection, endpoint, 2);
                plugin.OnEndpointConnectedAsync(connection, endpoint, CancellationToken.None).AsTask().Wait();
            }

            await Task.Delay(150);
            Assert.Empty(_handler.Requests);
        }

        [Fact]
        public async Task The_same_alert_is_held_back_for_a_while_and_counted()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h", ["FailuresBeforeDown"] = "1", ["MinSecondsBetween"] = "300" });
            var connection = new FakeConnection();
            var first = Endpoint();

            //An endpoint goes down and comes back again and again: only the first "down" is told within 5 minutes, but the recoveries all are.
            for (var i = 0; i < 4; i++)
            {
                Fail(plugin, connection, first, 1);
                plugin.OnEndpointConnectedAsync(connection, first, CancellationToken.None).AsTask().Wait();
            }

            var sent = await Sent(5);
            await Task.Delay(100);
            sent = _handler.Requests.ToList();

            var bodies = sent.Select(o => JsonDocument.Parse(o.Body).RootElement).ToList();
            Assert.Equal(1, bodies.Count(o => o.GetProperty("severity").GetString() == "Critical"));
            Assert.Equal(4, bodies.Count(o => o.GetProperty("severity").GetString() == "Recovered"));

            Clock.Advance(TimeSpan.FromMinutes(6));
            Fail(plugin, connection, first, 1);
            sent = await Sent(6);
            Assert.Contains("3 more like this were held back", JsonDocument.Parse(sent.Last().Body).RootElement.GetProperty("text").GetString());
        }

        [Fact]
        public async Task The_webhook_gets_json_and_only_the_webhook_gets_the_authorization()
        {
            var (plugin, _) = Start(new()
            {
                ["Webhook.Url"] = "https://hooks.example/h",
                ["Webhook.Authorization"] = "Bearer secret-token",
                ["Discord.Url"] = "https://discord.example/d",
                ["Source"] = "web01",
                ["FailuresBeforeDown"] = "1"
            });

            Fail(plugin, new FakeConnection(), Endpoint(), 1);
            var sent = await Sent(2);

            var webhook = sent.Single(o => o.Url == "https://hooks.example/h");
            var json = JsonDocument.Parse(webhook.Body).RootElement;
            Assert.Equal("NetProxy on web01", json.GetProperty("source").GetString());
            Assert.Equal("Critical", json.GetProperty("severity").GetString());
            Assert.StartsWith("endpoint:", json.GetProperty("key").GetString());
            Assert.Equal("Bearer secret-token", webhook.Authorization);
            Assert.Null(sent.Single(o => o.Url == "https://discord.example/d").Authorization);
        }

        [Fact]
        public async Task A_proxy_that_has_no_working_endpoint_is_reported_with_what_was_tried()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h" });
            var failures = new[] { new NpEndpointFailure(Endpoint(8080), "refused"), new NpEndpointFailure(Endpoint(8081), "timed out") };

            plugin.OnEndpointsUnavailableAsync(new NpUnavailableEventArgs(new FakeConnection(), failures, true), CancellationToken.None).AsTask().Wait();
            var sent = await Sent(1);

            var text = JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("text").GetString()!;
            Assert.Contains("10.0.0.5:8080 (refused)", text);
            Assert.Contains("10.0.0.5:8081 (timed out)", text);
        }

        [Fact]
        public async Task A_proxy_that_does_not_start_is_reported_and_so_is_the_start_that_follows()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h" });
            var proxy = new NpProxyInfo(Guid.NewGuid(), "web", 443, NpTrafficKind.Https);

            plugin.OnProxyStartedAsync(proxy, ["0.0.0.0:443"], CancellationToken.None).AsTask().Wait(); //A normal start: nothing to tell.
            plugin.OnProxyStartFailedAsync(proxy, "Address already in use", CancellationToken.None).AsTask().Wait();
            await Sent(1);
            plugin.OnProxyStartedAsync(proxy, ["0.0.0.0:443"], CancellationToken.None).AsTask().Wait();
            var sent = await Sent(2);

            Assert.Contains("Address already in use", JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("text").GetString());
            Assert.Equal("Recovered", JsonDocument.Parse(sent[1].Body).RootElement.GetProperty("severity").GetString());
        }

        [Fact]
        public async Task Failed_logins_are_reported_when_there_are_enough_of_them_close_together()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h" });
            var failed = new NpLoginInfo("admin", false);

            plugin.OnManagementLoginAsync(new NpLoginInfo("admin", true), CancellationToken.None).AsTask().Wait();
            plugin.OnManagementLoginAsync(failed, CancellationToken.None).AsTask().Wait();
            Clock.Advance(TimeSpan.FromMinutes(11)); //Too far apart to count together.
            plugin.OnManagementLoginAsync(failed, CancellationToken.None).AsTask().Wait();
            plugin.OnManagementLoginAsync(failed, CancellationToken.None).AsTask().Wait();
            await Task.Delay(100);
            Assert.Empty(_handler.Requests);

            plugin.OnManagementLoginAsync(new NpLoginInfo("root", false), CancellationToken.None).AsTask().Wait();
            var sent = await Sent(1);

            Assert.Contains("3 failed logins", JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("text").GetString());
            Assert.Contains("'root'", JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("text").GetString());
        }

        [Fact]
        public async Task Errors_are_reported_unless_switched_off()
        {
            var (plugin, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/h" });
            plugin.OnErrorAsync(new NpPluginError("Connection abc failed", null, new NpProxyInfo(Guid.NewGuid(), "web", 443, NpTrafficKind.Https), null), CancellationToken.None).AsTask().Wait();
            var sent = await Sent(1);
            Assert.Contains("Connection abc failed", JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("text").GetString());

            var (quiet, _) = Start(new() { ["Webhook.Url"] = "https://hooks.example/other", ["AlertOnErrors"] = "false" });
            quiet.OnErrorAsync(new NpPluginError("x", null, null, null), CancellationToken.None).AsTask().Wait();
            await Task.Delay(100);
            Assert.DoesNotContain(_handler.Requests, o => o.Url.EndsWith("/other"));
        }

        [Fact]
        public async Task A_channel_that_fails_is_logged_and_the_others_still_get_the_alert()
        {
            _handler.Status = HttpStatusCode.InternalServerError;
            var (plugin, host) = Start(new() { ["Discord.Url"] = "https://discord.example/d", ["Slack.Url"] = "https://hooks.example/s", ["FailuresBeforeDown"] = "1" });

            Fail(plugin, new FakeConnection(), Endpoint(), 1);
            await Sent(2);
            await Eventually(() => host.Texts.Count(o => o.Contains("could not be sent")), n => n >= 2);

            Assert.Equal(2, host.Texts.Count(o => o.Contains("could not be sent") && o.Contains("500")));
        }

        [Fact]
        public void E_mail_is_built_for_the_people_it_is_set_for()
        {
            var (plugin, _) = Start(new()
            {
                ["Smtp.Host"] = "mail.example",
                ["Smtp.From"] = "netproxy@example.com",
                ["Smtp.To"] = "ops@example.com, boss@example.com",
                ["FailuresBeforeDown"] = "1",
                ["Source"] = "web01"
            });

            Fail(plugin, new FakeConnection(), Endpoint(), 1);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (_mail.Count == 0 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }

            var mail = Assert.Single(_mail);
            Assert.Equal("netproxy@example.com", mail.From!.Address);
            Assert.Equal("[NetProxy on web01] Endpoint down: 10.0.0.5:8080", mail.Subject);
            Assert.Contains("refused", mail.Body);

            var channel = new SmtpChannel(new SmtpSettings("h", 25, null, null, "a@b.com", ["x@y.com", "z@y.com"], false));
            using var built = channel.Build(new AlertMessage("k", AlertSeverity.Warning, "T", "Text", DateTime.UtcNow), "src");
            Assert.Equal(2, built.To.Count);
        }

        [Fact]
        public void Settings_that_are_wrong_are_reported_and_nothing_is_sent_without_a_place_to_send_to()
        {
            var (plugin, host) = Start(new() { ["Discord.Url"] = "not a url", ["Smtp.Host"] = "mail.example", ["Smtp.To"] = "ops@example.com", ["FailuresBeforeDown"] = "0" });

            Assert.Contains(host.Texts, o => o.Contains("Discord.Url") && o.Contains("not used"));
            Assert.Contains(host.Texts, o => o.StartsWith("E-mail needs Smtp.Host"));
            Assert.Contains(host.Texts, o => o.Contains("FailuresBeforeDown"));
            Assert.Contains(host.Texts, o => o.StartsWith("No place to send alerts to"));

            Fail(plugin, new FakeConnection(), Endpoint(), 5);
            Thread.Sleep(100);
            Assert.Empty(_handler.Requests);
        }

        [Fact]
        public async Task The_start_can_be_announced()
        {
            Start(new() { ["Webhook.Url"] = "https://hooks.example/h", ["NotifyOnStart"] = "true", ["Source"] = "web01" });

            var sent = await Sent(1);
            Assert.Equal("NetProxy started", JsonDocument.Parse(sent[0].Body).RootElement.GetProperty("title").GetString());
        }
    }
}
