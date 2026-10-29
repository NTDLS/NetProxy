using NetProxy.PluginApi;
using NetProxy.Plugins.AccessLog;
using NetProxy.Plugins.Tests.Support;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace NetProxy.Plugins.Tests
{
    public class AccessLogTests : PluginTestBase
    {
        private string _directory = string.Empty;

        private (AccessLogPlugin Plugin, FakeHost Host) Start(Dictionary<string, string>? settings = null)
        {
            _directory = TempDirectory();
            settings ??= [];
            settings.TryAdd("Directory", Path.Combine(_directory, "logs"));
            return Start(new AccessLogPlugin(Clock), settings, _directory);
        }

        private static byte[] Response(int status, string body = "hello") => Http.Bytes($"HTTP/1.1 {status} X\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\n\r\n{body}");

        private static void Exchange(AccessLogPlugin plugin, FakeConnection connection, byte[] request, int status = 200, string body = "hello")
        {
            Send(plugin, connection, request);
            Send(plugin, connection, Response(status, body), direction: NpDataDirection.EndpointToClient);
        }

        /// <summary>Stops the plug-in (which writes what is queued) and reads the lines of all the files, with the time of each masked.</summary>
        private List<string> Lines(AccessLogPlugin plugin)
        {
            plugin.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();
            var logs = Path.Combine(_directory, "logs");
            return Directory.Exists(logs)
                ? Directory.EnumerateFiles(logs).OrderBy(o => o).SelectMany(File.ReadAllLines).Select(o => Regex.Replace(o, @"\[[^\]]+\]", "[TIME]")).ToList()
                : [];
        }

        [Fact]
        public void A_request_and_its_response_make_one_combined_line()
        {
            var (plugin, _) = Start();
            var connection = new FakeConnection { ClientAddress = "203.0.113.7" };

            Exchange(plugin, connection, Http.Request("/products?id=7", extraHeaders: "User-Agent: Mozilla/5.0 Firefox/121\r\nReferer: https://google.com/\r\n"), body: "<html>");

            Assert.Equal("203.0.113.7 - - [TIME] \"GET /products?id=7 HTTP/1.1\" 200 6 \"https://google.com/\" \"Mozilla/5.0 Firefox/121\"", Assert.Single(Lines(plugin)));
        }

        [Fact]
        public void The_requests_of_a_keep_alive_connection_each_get_the_status_of_their_own_response()
        {
            var (plugin, _) = Start(new() { ["Format"] = "common" });
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/a").Concat(Http.Request("/b")).Concat(Http.Request("/c")).ToArray());
            Send(plugin, connection, Response(200).Concat(Response(404)).Concat(Response(500, "")).ToArray(), direction: NpDataDirection.EndpointToClient);

            Assert.Equal(
            [
                "203.0.113.7 - - [TIME] \"GET /a HTTP/1.1\" 200 5",
                "203.0.113.7 - - [TIME] \"GET /b HTTP/1.1\" 404 5",
                "203.0.113.7 - - [TIME] \"GET /c HTTP/1.1\" 500 0"
            ], Lines(plugin));
        }

        [Fact]
        public void A_request_that_was_never_answered_is_logged_when_the_connection_ends()
        {
            var (plugin, _) = Start(new() { ["Format"] = "common" });
            var connection = new FakeConnection();

            Send(plugin, connection, Http.Request("/slow"));
            plugin.OnDisconnectedAsync(connection, new NpDisconnectInfo(NpDisconnectReason.Aborted, TimeSpan.Zero, 0, 0), CancellationToken.None).AsTask().Wait();

            Assert.Equal("203.0.113.7 - - [TIME] \"GET /slow HTTP/1.1\" - -", Assert.Single(Lines(plugin)));
        }

        [Fact]
        public void Json_has_a_line_per_request_with_the_fields()
        {
            var (plugin, _) = Start(new() { ["Format"] = "json" });

            Exchange(plugin, new FakeConnection(), Http.Request("/p?q=1", "POST", "WWW.Example.com:443", "User-Agent: curl/8\r\n", "abc"), 201, "ok");

            var json = JsonDocument.Parse(Assert.Single(Lines(plugin))).RootElement;
            Assert.Equal("203.0.113.7", json.GetProperty("client").GetString());
            Assert.Equal("www.example.com", json.GetProperty("host").GetString());
            Assert.Equal("POST", json.GetProperty("method").GetString());
            Assert.Equal("/p", json.GetProperty("path").GetString());
            Assert.Equal("?q=1", json.GetProperty("query").GetString());
            Assert.Equal(201, json.GetProperty("status").GetInt32());
            Assert.Equal(2, json.GetProperty("bytes").GetInt64());
            Assert.Equal(3, json.GetProperty("requestBytes").GetInt64());
            Assert.Equal("curl/8", json.GetProperty("userAgent").GetString());
            Assert.True(json.GetProperty("tls").GetBoolean());
            Assert.Equal("web", json.GetProperty("proxy").GetString());
        }

        [Fact]
        public void A_client_can_not_end_the_quotes_or_forge_a_line()
        {
            Assert.Equal("a\\\"b\\\\c\\x07d\\x0a", AccessLogFormatter.Quoted("a\"b\\c\ad\n"));

            var (plugin, _) = Start();
            Exchange(plugin, new FakeConnection(), Http.Request("/x", extraHeaders: "User-Agent: evil\" 200 0 \"-\" \"fake\r\n"));

            var line = Assert.Single(Lines(plugin));
            Assert.EndsWith("\"evil\\\" 200 0 \\\"-\\\" \\\"fake\"", line);
        }

        [Fact]
        public void Behind_a_trusted_proxy_the_client_is_logged_and_not_the_proxy()
        {
            var (plugin, _) = Start(new() { ["Format"] = "common", ["TrustedProxies"] = "cloudflare" });

            Exchange(plugin, new FakeConnection { ClientAddress = "104.16.1.1" }, Http.Request("/a", extraHeaders: "CF-Connecting-IP: 198.51.100.9\r\n"));
            Exchange(plugin, new FakeConnection { ClientAddress = "203.0.113.50" }, Http.Request("/b", extraHeaders: "CF-Connecting-IP: 198.51.100.77\r\n"));

            var lines = Lines(plugin);
            Assert.StartsWith("198.51.100.9 ", lines[0]);
            Assert.StartsWith("203.0.113.50 ", lines[1]); //Not a trusted proxy, so what it says is not believed.
        }

        [Fact]
        public void Paths_and_clients_can_be_left_out()
        {
            var (plugin, _) = Start(new() { ["Format"] = "common", ["IgnorePathPrefixes"] = "/health, /static/", ["IgnoreClients"] = "10.0.0.9" });

            Exchange(plugin, new FakeConnection(), Http.Request("/health"));
            Exchange(plugin, new FakeConnection(), Http.Request("/Static/app.js"));
            Exchange(plugin, new FakeConnection { ClientAddress = "10.0.0.9" }, Http.Request("/page"));
            Exchange(plugin, new FakeConnection(), Http.Request("/page"));

            Assert.Equal("203.0.113.7 - - [TIME] \"GET /page HTTP/1.1\" 200 5", Assert.Single(Lines(plugin)));
        }

        [Fact]
        public void Each_day_has_a_file_and_a_file_that_gets_too_big_is_followed_by_another()
        {
            var (plugin, _) = Start(new() { ["Format"] = "common", ["MaxFileMB"] = "1" });
            var connection = new FakeConnection();

            Exchange(plugin, connection, Http.Request("/day-one"));

            //The line is written by a task of its own: wait for it before the day changes.
            var first = Path.Combine(_directory, "logs", "access-2026-01-01.log");
            for (var i = 0; i < 200 && (File.Exists(first) == false || new FileInfo(first).Length == 0); i++)
            {
                Thread.Sleep(10);
            }

            Clock.Advance(TimeSpan.FromDays(1));
            Exchange(plugin, connection, Http.Request("/day-two"));
            plugin.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();

            var files = Directory.EnumerateFiles(Path.Combine(_directory, "logs")).Select(Path.GetFileName).OrderBy(o => o).ToList();
            Assert.Equal(["access-2026-01-01.log", "access-2026-01-02.log"], files);
            Assert.Contains("/day-one", File.ReadAllText(Path.Combine(_directory, "logs", files[0]!)));
            Assert.Contains("/day-two", File.ReadAllText(Path.Combine(_directory, "logs", files[1]!)));
        }

        [Fact]
        public void Old_files_are_removed_and_the_ones_that_are_not_old_stay()
        {
            var directory = TempDirectory();
            var logs = Path.Combine(directory, "logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "access-2025-06-01.log"), "old");
            File.SetLastWriteTimeUtc(Path.Combine(logs, "access-2025-06-01.log"), new DateTime(2025, 6, 1));
            File.WriteAllText(Path.Combine(logs, "access-2025-12-30.log"), "recent");
            File.SetLastWriteTimeUtc(Path.Combine(logs, "access-2025-12-30.log"), new DateTime(2025, 12, 30));
            File.WriteAllText(Path.Combine(logs, "other.txt"), "mine");

            var (plugin, _) = Start(new AccessLogPlugin(Clock), new() { ["Directory"] = logs, ["KeepDays"] = "30" }, directory);
            Exchange(plugin, new FakeConnection(), Http.Request("/a"));
            plugin.OnServiceStoppingAsync(CancellationToken.None).AsTask().Wait();

            Assert.False(File.Exists(Path.Combine(logs, "access-2025-06-01.log")));
            Assert.True(File.Exists(Path.Combine(logs, "access-2025-12-30.log")));
            Assert.True(File.Exists(Path.Combine(logs, "other.txt")));
        }

        [Fact]
        public void Http2_tcp_and_udp_proxies_are_not_logged()
        {
            var (plugin, host) = Start();

            Exchange(plugin, new FakeConnection { IsHttp2 = true }, Http.Request("/a"));
            Exchange(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "raw", 5000, NpTrafficKind.Tcp) }, Http.Request("/b"));
            Exchange(plugin, new FakeConnection { Proxy = new(Guid.NewGuid(), "u", 5000, NpTrafficKind.Udp) }, Http.Request("/c"));

            Assert.Empty(Lines(plugin));
            Assert.Single(host.Texts, o => o.Contains("HTTP/2"));
        }

        [Fact]
        public void Settings_that_are_wrong_are_reported()
        {
            var (_, host) = Start(new() { ["Format"] = "xml", ["FileName"] = "log.txt", ["TrustedProxies"] = "nonsense" });

            Assert.Contains(host.Texts, o => o.Contains("Format must be"));
            Assert.Contains(host.Texts, o => o.Contains("FileName must be"));
            Assert.Contains(host.Texts, o => o.Contains("'nonsense'"));
        }
    }
}
