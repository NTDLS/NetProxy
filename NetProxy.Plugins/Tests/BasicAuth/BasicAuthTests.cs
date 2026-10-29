using NetProxy.PluginApi;
using NetProxy.Plugins.BasicAuth;
using NetProxy.Plugins.Tests.Support;
using System.Text;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class BasicAuthTests : PluginTestBase
    {
        private (BasicAuthPlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
        {
            settings ??= [];
            settings.TryAdd("User1", "alice:wonderland");
            return Start(new BasicAuthPlugin(Clock), settings);
        }

        private static string Login(string user, string password) => "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")) + "\r\n";

        private static NpDataEventArgs Get(BasicAuthPlugin plugin, FakeConnection connection, string path = "/admin", string headers = "")
            => Send(plugin, connection, Http.Request(path, extraHeaders: headers));

        [Fact]
        public void A_request_without_a_login_is_asked_for_one_and_never_reaches_the_endpoint()
        {
            var (plugin, _) = Start();

            var data = Get(plugin, new FakeConnection());

            Assert.True(data.IsDropped);
            Assert.False(data.CloseAfterResponse);
            var (head, _) = Split(data.Response);
            Assert.StartsWith("HTTP/1.1 401 Unauthorized\r\n", head);
            Assert.Contains("WWW-Authenticate: Basic realm=\"Restricted\", charset=\"UTF-8\"", head);
            Assert.Contains("Connection: keep-alive", head);
        }

        [Fact]
        public void A_right_login_goes_through_and_a_wrong_one_does_not()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();

            Assert.False(Get(plugin, connection, headers: Login("alice", "wonderland")).IsDropped);
            Assert.True(Get(plugin, connection, headers: Login("alice", "wrong")).IsDropped);
            Assert.True(Get(plugin, connection, headers: Login("mallory", "wonderland")).IsDropped);
            Assert.True(Get(plugin, connection, headers: Login("alice", "")).IsDropped);
            Assert.True(Get(plugin, connection, headers: "Authorization: Basic !!!not-base64\r\n").IsDropped);
            Assert.True(Get(plugin, connection, headers: "Authorization: Bearer abc\r\n").IsDropped);
            Assert.False(Get(plugin, connection, headers: Login("alice", "wonderland")).IsDropped);
        }

        [Fact]
        public void A_password_can_have_a_colon_and_the_realm_can_be_set()
        {
            var (plugin, _) = Start(new() { ["User1"] = "bob:pass:with:colons", ["Realm"] = "Staff \"only\"" });

            Assert.False(Get(plugin, new FakeConnection(), headers: Login("bob", "pass:with:colons")).IsDropped);
            Assert.Contains("realm=\"Staff 'only'\"", Split(Get(plugin, new FakeConnection()).Response).Head);
        }

        [Fact]
        public void Passwords_can_be_kept_as_hashes()
        {
            var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("secret1"))).ToLowerInvariant();
            var (plugin, host) = Start(new() { ["User1"] = "carol:sha256:" + sha, ["User2"] = "dave:" + PasswordCheck.Hash("secret2", 1000) });

            Assert.False(Get(plugin, new FakeConnection(), headers: Login("carol", "secret1")).IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), headers: Login("carol", "secret2")).IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), headers: Login("dave", "secret2")).IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), headers: Login("dave", "secret1")).IsDropped);
            Assert.DoesNotContain(host.Messages, o => o.Level == NpPluginLogLevel.Warning);
        }

        [Fact]
        public void Users_that_are_not_right_are_reported_and_left_out()
        {
            var (plugin, host) = Start(new()
            {
                ["User1"] = "alice:wonderland", ["User2"] = "alice:again", ["User3"] = "nocolon", ["User4"] = "eve:sha256:zz", ["User5"] = "frank:pbkdf2:x:y:z"
            });

            Assert.Equal(4, host.Messages.Count(o => o.Level == NpPluginLogLevel.Warning && o.Message.Contains("is left out")));
            Assert.False(Get(plugin, new FakeConnection(), headers: Login("alice", "wonderland")).IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), headers: Login("eve", "zz")).IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), headers: Login("alice", "again")).IsDropped);
        }

        [Fact]
        public void Without_any_users_nobody_gets_in_and_the_log_says_so()
        {
            var (plugin, host) = Start(new BasicAuthPlugin(Clock), new());

            Assert.Contains(host.Texts, o => o.StartsWith("There are no users"));
            Assert.True(Get(plugin, new FakeConnection(), headers: Login("alice", "x")).IsDropped);
        }

        [Fact]
        public void Only_the_paths_that_are_asked_for_need_a_login()
        {
            var (plugin, _) = Start(new() { ["Paths"] = "/admin, /tools", ["ExcludePaths"] = "/admin/public" });

            Assert.True(Get(plugin, new FakeConnection(), "/admin/users").IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), "/TOOLS").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/index.html").IsDropped);
            Assert.False(Get(plugin, new FakeConnection(), "/admin/public/readme.txt").IsDropped);
            Assert.True(Get(plugin, new FakeConnection(), "/administrator").IsDropped); // A prefix: it starts with /admin.
        }

        [Fact]
        public void The_query_string_and_the_host_do_not_change_what_is_protected()
        {
            var (plugin, _) = Start(new() { ["Paths"] = "/admin", ["Hosts"] = "*.example.com" });

            Assert.True(Send(plugin, new FakeConnection(), Http.Request("/admin?x=/public", host: "www.example.com")).IsDropped);
            Assert.False(Send(plugin, new FakeConnection(), Http.Request("/admin", host: "www.other.org")).IsDropped);
            Assert.True(Send(plugin, new FakeConnection(), Http.Request("http://www.example.com/admin", host: "www.example.com")).IsDropped);
        }

        [Fact]
        public void Clients_in_allow_need_no_login()
        {
            var (plugin, _) = Start(new() { ["Allow"] = "10.0.0.0/8, 198.51.100.9" });

            Assert.False(Get(plugin, new FakeConnection { ClientAddress = "10.1.2.3" }).IsDropped);
            Assert.False(Get(plugin, new FakeConnection { ClientAddress = "198.51.100.9" }).IsDropped);
            Assert.True(Get(plugin, new FakeConnection { ClientAddress = "198.51.100.10" }).IsDropped);
        }

        [Fact]
        public void Behind_cloudflare_the_client_is_the_one_in_the_header_and_only_if_cloudflare_says_so()
        {
            var (plugin, _) = Start(new() { ["Allow"] = "198.51.100.9", ["TrustedProxies"] = "cloudflare" });

            Assert.False(Send(plugin, new FakeConnection { ClientAddress = "104.16.1.1" }, Http.Request("/admin", extraHeaders: "CF-Connecting-IP: 198.51.100.9\r\n")).IsDropped);
            Assert.True(Send(plugin, new FakeConnection { ClientAddress = "203.0.113.50" }, Http.Request("/admin", extraHeaders: "CF-Connecting-IP: 198.51.100.9\r\n")).IsDropped);
        }

        [Fact]
        public void Too_many_wrong_passwords_lock_an_address_out_even_for_the_right_one_and_then_it_ends()
        {
            var (plugin, host) = Start(new() { ["MaxFailures"] = "3", ["LockoutMinutes"] = "5" });
            var other = new FakeConnection { ClientAddress = "198.51.100.9" };

            for (var i = 0; i < 3; i++)
            {
                Assert.StartsWith("HTTP/1.1 401", Http.Text(Get(plugin, new FakeConnection(), headers: Login("alice", "wrong" + i)).Response));
            }

            var locked = Get(plugin, new FakeConnection(), headers: Login("alice", "wonderland"));
            Assert.True(locked.IsDropped);
            Assert.True(locked.CloseAfterResponse);
            Assert.StartsWith("HTTP/1.1 429", Http.Text(locked.Response));
            Assert.Contains("Retry-After: 300", Http.Text(locked.Response));
            Assert.Contains(host.Texts, o => o.Contains("locked out for 5 minute(s)"));

            Assert.False(Get(plugin, other, headers: Login("alice", "wonderland")).IsDropped); //Somebody else is fine.

            Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
            Assert.False(Get(plugin, new FakeConnection(), headers: Login("alice", "wonderland")).IsDropped);
        }

        [Fact]
        public void Asking_for_a_login_is_not_a_wrong_password_and_failures_far_apart_do_not_add_up()
        {
            var (plugin, _) = Start(new() { ["MaxFailures"] = "3", ["FailureWindowMinutes"] = "10" });

            for (var i = 0; i < 20; i++)
            {
                Get(plugin, new FakeConnection()); //No login at all: a browser that has not asked the user yet.
            }

            for (var i = 0; i < 10; i++)
            {
                Get(plugin, new FakeConnection(), headers: Login("alice", "wrong"));
                Clock.Advance(TimeSpan.FromMinutes(6));
                plugin.OnTickAsync(CancellationToken.None).AsTask().Wait();
            }

            Assert.False(Get(plugin, new FakeConnection(), headers: Login("alice", "wonderland")).IsDropped);
        }

        [Fact]
        public void A_head_that_arrives_in_pieces_is_held_back_and_a_body_does_not_keep_the_connection()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            var bytes = Http.Request("/admin", extraHeaders: Login("alice", "wonderland"));

            var first = Send(plugin, connection, bytes.AsSpan(0, 15).ToArray());
            Assert.True(first.IsDropped); //Nothing of a half request goes on.
            Assert.Null(first.Response);

            var second = Send(plugin, connection, bytes.AsSpan(15).ToArray());
            Assert.False(second.IsDropped);
            Assert.Equal(bytes, second.Data.ToArray()); //Whole, in one piece.

            var post = Http.Request("/admin", "POST", body: "0123456789");
            Assert.True(Send(plugin, new FakeConnection(), post.AsSpan(0, post.Length - 5).ToArray()).CloseAfterResponse);
        }

        [Fact]
        public void A_second_request_on_the_connection_is_checked_too_also_after_a_body()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection();
            var login = Login("alice", "wonderland");

            Assert.False(Send(plugin, connection, Http.Request("/public-ok-because-allowed-login", "POST", extraHeaders: login, body: "hello")).IsDropped);
            Assert.True(Get(plugin, connection).IsDropped); //No login on this one.

            var chunked = new FakeConnection();
            Assert.False(Send(plugin, chunked, Http.Request("/up", "POST", extraHeaders: login + "Transfer-Encoding: chunked\r\n")).IsDropped);
            Assert.False(Send(plugin, chunked, Http.Bytes("5\r\nhello\r\n0\r\n\r\n")).IsDropped);
            Assert.True(Get(plugin, chunked).IsDropped);
        }

        [Fact]
        public void Pipelined_requests_are_all_checked_and_one_without_a_login_closes_the_connection()
        {
            var (plugin, _) = Start();
            var login = Login("alice", "wonderland");

            var allowed = Send(plugin, new FakeConnection(), Http.Request("/a", extraHeaders: login).Concat(Http.Request("/b", extraHeaders: login)).ToArray());
            Assert.False(allowed.IsDropped);

            var mixed = Send(plugin, new FakeConnection(), Http.Request("/a", extraHeaders: login).Concat(Http.Request("/b")).ToArray());
            Assert.True(mixed.IsDropped);
            Assert.True(mixed.CloseAfterResponse);
            Assert.Contains("Connection: close", Split(mixed.Response).Head);
        }

        [Fact]
        public void What_can_not_be_checked_does_not_get_through()
        {
            var (plugin, host) = Start();

            var h2 = Send(plugin, new FakeConnection { IsHttp2 = true }, Http.Request("/admin"), canRespond: false);
            Assert.True(h2.IsDropped);
            Assert.NotNull(h2.CloseReason);
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));

            var binary = Send(plugin, new FakeConnection(), [0x16, 0x03, 0x01, 0x02, 0x00, 0x01, 0x00]);
            Assert.True(binary.IsDropped);
            Assert.NotNull(binary.CloseReason);

            var cannotAnswer = Send(plugin, new FakeConnection(), Http.Request("/admin"), canRespond: false);
            Assert.True(cannotAnswer.IsDropped);
            Assert.NotNull(cannotAnswer.CloseReason);
        }

        [Fact]
        public void An_upgraded_connection_that_was_let_in_carries_on_and_other_traffic_is_left_alone()
        {
            var (plugin, _) = Start();
            var socket = new FakeConnection();

            Assert.True(Get(plugin, new FakeConnection(), headers: "Upgrade: websocket\r\nConnection: Upgrade\r\n").IsDropped); //Not logged in.
            Assert.False(Get(plugin, socket, headers: Login("alice", "wonderland") + "Upgrade: websocket\r\nConnection: Upgrade\r\n").IsDropped);
            Assert.False(Send(plugin, socket, [0x81, 0x05, 0x68, 0x65, 0x6c, 0x6c, 0x6f]).IsDropped); //The frames of the WebSocket.

            Assert.False(Send(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) }, Http.Request("/admin")).IsDropped);
            Assert.False(Send(plugin, new FakeConnection(), Http.Request("/admin"), direction: NpDataDirection.EndpointToClient).IsDropped);
        }

        [Fact]
        public void The_check_of_a_password_handles_odd_input()
        {
            Assert.True(PasswordCheck.Verify("plain", "plain"));
            Assert.False(PasswordCheck.Verify("plain", "Plain"));
            Assert.False(PasswordCheck.Verify("sha256:nothex", "x"));
            Assert.False(PasswordCheck.Verify("pbkdf2:1", "x"));
            Assert.False(PasswordCheck.Verify("pbkdf2:abc:AAAA:AAAA", "x"));
            Assert.True(PasswordCheck.Verify(PasswordCheck.Hash("h", 1000), "h"));
            Assert.NotEqual(PasswordCheck.Hash("h", 1000), PasswordCheck.Hash("h", 1000)); //A salt of its own each time.
        }
    }
}
