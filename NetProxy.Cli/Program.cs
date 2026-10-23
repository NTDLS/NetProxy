using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NetProxy.ClientApi;
using NetProxy.Library.Payloads;
using NetProxy.Library.Payloads.ReliableMessages.Queries;
using NetProxy.Library.Payloads.Routing;
using System.Text;

namespace NetProxy.Cli
{
    internal static class Program
    {
        private const string Usage = """
            netproxyctl - manage a NetProxy service from the command line

            Usage: netproxyctl <command> [options]

            Connecting (every command)
              login --server <host> [--port 5854] --user <name> [--password-stdin]
                                           Keeps the login for your user. Or set NETPROXY_SERVER, NETPROXY_PORT,
                                           NETPROXY_USER and NETPROXY_PASSWORD, or give --server/--port/--user/--password.
              logout                       Forgets the login.

            Service
              status                       Version, uptime and counts.
              reload [--dry-run]           Applies changes made to the configuration files by hand. Proxies that did not change
                                           keep running. Also done by kill -HUP on Linux and macOS.
              validate                     reload --dry-run: checks the files and says what a reload would do.
              logs [-n 100]                The end of the service log.
              certs                        The TLS certificates and when they expire.
              stats                        Traffic of each proxy.
              export [-o file]             The proxies, plugins and settings as one JSON document.
              import -f file [--dry-run]   Applies such a document (proxies and plugins by name).

            Proxies (matched by name)
              proxy list
              proxy get <name> [--show-secrets]
              proxy apply -f <file|-> [--dry-run]   Adds or replaces proxies. The file holds a proxy, a list of proxies or an export.
              proxy validate -f <file|->
              proxy start|stop|restart|delete <name>
              connections [<proxy>]        Open connections.
              connections close <proxy> | --id <connection id>

            Users
              user list
              user add <name> [--password-stdin] [--description text]
              user passwd <name> [--password-stdin]
              user remove <name>

            Plugins
              plugin list
              plugin apply -f <file|->     Adds or replaces plugins by name.
              plugin remove|reload|up|down <name>

            Settings
              settings get
              settings set -f <file|->

            Options: --json for machine readable output. Exit codes: 0 done, 1 not done, 2 wrong command line, 3 cannot connect or log in.
            """;

        private static int Main(string[] argv)
        {
            try
            {
                var arguments = new Arguments(argv);

                if (arguments.Words.Count == 0 || arguments.Has("help") || arguments.Words[0].Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Usage);
                    return arguments.Words.Count == 0 && arguments.Has("help") == false ? 2 : 0;
                }

                return Run(arguments);
            }
            catch (UsageException ex)
            {
                Console.Error.WriteLine($"{ex.Message} (netproxyctl help)");
                return 2;
            }
            catch (CommandFailedException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ex.ExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetBaseException().Message);
                return 1;
            }
        }

        private static int Run(Arguments a)
        {
            var command = a.Words[0].ToLowerInvariant();

            switch (command)
            {
                case "login":
                    return Login(a);

                case "logout":
                    if (File.Exists(Credentials.FilePath))
                    {
                        File.Delete(Credentials.FilePath);
                    }
                    Console.WriteLine("Logged out.");
                    return 0;
            }

            using var client = Connect(a);

            switch (command)
            {
                case "status": return Status(client, a);
                case "reload": return Reload(client, a, a.Has("dry-run"));
                case "validate": return Reload(client, a, dryRun: true);
                case "logs": return Logs(client, a);
                case "certs": return Certificates(client, a);
                case "stats": return Statistics(client, a);
                case "export": return Export(client, a);
                case "import": return Import(client, a);
                case "proxy": return Proxy(client, a);
                case "connections": return Connections(client, a);
                case "user": return Users(client, a);
                case "plugin": return Plugins(client, a);
                case "settings": return Settings(client, a);
                default: throw new UsageException($"Unknown command '{a.Words[0]}'.");
            }
        }

        //---------------------------------------------------------------- Connecting

        private static NpServiceClient Connect(Arguments a)
        {
            var options = Credentials.Resolve(a);

            try
            {
                return NpServiceClient.Connect(options);
            }
            catch (Exception ex)
            {
                throw new CommandFailedException($"Could not connect to {options.Server}:{options.Port} as '{options.UserName}': {ex.GetBaseException().Message}", 3);
            }
        }

        private static int Login(Arguments a)
        {
            var saved = Credentials.Load();
            var user = a.Get("user") ?? Environment.GetEnvironmentVariable("NETPROXY_USER") ?? saved?.UserName
                ?? throw new UsageException("Missing --user.");
            var server = a.Get("server") ?? Environment.GetEnvironmentVariable("NETPROXY_SERVER") ?? saved?.Server ?? "localhost";
            var port = int.TryParse(a.Get("port") ?? Environment.GetEnvironmentVariable("NETPROXY_PORT"), out var parsed) ? parsed : saved?.Port ?? NetProxy.Library.Constants.DefaultManagementPort;

            var password = a.Get("password") ?? Environment.GetEnvironmentVariable("NETPROXY_PASSWORD")
                ?? (a.Has("password-stdin") ? Input.ReadLine() : ReadPassword($"Password for {user}@{server}: "));

            var options = new NpConnectionOptions { Server = server, Port = port, UserName = user, PasswordHash = NpConnectionOptions.HashPassword(password ?? string.Empty) };

            try
            {
                using var client = NpServiceClient.Connect(options);
            }
            catch (Exception ex)
            {
                throw new CommandFailedException($"Could not log in to {server}:{port} as '{user}': {ex.GetBaseException().Message}", 3);
            }

            new Credentials { Server = server, Port = port, UserName = user, PasswordHash = options.PasswordHash }.Save();
            Console.WriteLine($"Logged in to {server}:{port} as {user}. The login is kept in {Credentials.FilePath}.");
            return 0;
        }

        /// <summary>
        /// A password that is typed is not shown. A password that is piped in is read as a line.
        /// </summary>
        private static string ReadPassword(string prompt)
        {
            if (Console.IsInputRedirected)
            {
                return Input.ReadLine() ?? string.Empty;
            }

            Console.Error.Write(prompt);
            var password = new StringBuilder();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.Error.WriteLine();
                    return password.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Length--;
                    }
                }
                else if (char.IsControl(key.KeyChar) == false)
                {
                    password.Append(key.KeyChar);
                }
            }
        }

        //---------------------------------------------------------------- Service

        private static int Status(NpServiceClient client, Arguments a)
        {
            var info = client.GetInfo();

            if (a.Has("json"))
            {
                Output.Json(info);
                return 0;
            }

            Console.WriteLine($"NetProxy {info.Version} on {info.OperatingSystem}");
            Console.WriteLine($"Up {TimeSpan.FromSeconds(info.UptimeSeconds):d\\.hh\\:mm\\:ss}, management port {info.ManagementPort}, configuration '{info.ConfigurationName}'");
            Console.WriteLine($"{info.RunningProxyCount} of {info.ProxyCount} proxies running, {info.PluginCount} plugins");
            return 0;
        }

        private static int Reload(NpServiceClient client, Arguments a, bool dryRun)
        {
            var reply = client.Reload(dryRun);

            if (a.Has("json"))
            {
                Output.Json(reply);
                return reply.Success ? 0 : 1;
            }

            foreach (var problem in reply.Problems)
            {
                Console.Error.WriteLine($"Problem: {problem}");
            }

            if (reply.Message != null)
            {
                Console.Error.WriteLine(reply.Message);
            }

            if (reply.Success == false)
            {
                Console.Error.WriteLine("Nothing was changed.");
                return 1;
            }

            foreach (var change in reply.Changes)
            {
                Console.WriteLine(dryRun ? $"Would change: {change}" : change);
            }

            if (reply.Changes.Count == 0)
            {
                Console.WriteLine("No changes.");
            }

            return 0;
        }

        private static int Logs(NpServiceClient client, Arguments a)
        {
            var lines = client.GetLog(int.TryParse(a.Get("lines"), out var count) ? count : 100, out var message);

            if (message != null)
            {
                Console.Error.WriteLine(message);
            }

            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }

            return message == null ? 0 : 1;
        }

        private static int Certificates(NpServiceClient client, Arguments a)
        {
            var certificates = client.GetCertificates();

            if (a.Has("json"))
            {
                Output.Json(certificates);
                return 0;
            }

            Output.Table(["PROXY", "HOST", "EXPIRES", "DAYS", "SUBJECT"],
                certificates.Select(o => new[]
                {
                    o.ProxyName, o.HostName.Length == 0 ? "(proxy)" : o.HostName, o.NotAfterUtc?.ToString("yyyy-MM-dd") ?? "-",
                    o.DaysLeft?.ToString() ?? "-", o.Error.Length > 0 ? $"ERROR: {o.Error}" : o.Subject
                }));

            return certificates.Any(o => o.Error.Length > 0 || o.DaysLeft < 0) ? 1 : 0;
        }

        private static int Statistics(NpServiceClient client, Arguments a)
        {
            var proxies = client.ListProxies();
            var statistics = client.GetStatistics();

            if (a.Has("json"))
            {
                Output.Json(statistics.Values);
                return 0;
            }

            Output.Table(["NAME", "STATE", "CONNECTIONS", "TOTAL", "BYTES IN", "BYTES OUT"],
                proxies.OrderBy(o => o.Name).Select(o =>
                {
                    statistics.TryGetValue(o.Id, out var s);
                    return new[]
                    {
                        o.Name, o.IsRunning ? "running" : "stopped", $"{s?.CurrentConnections ?? 0}", $"{s?.TotalConnections ?? 0}",
                        Output.Bytes(s?.BytesRead ?? 0), Output.Bytes(s?.BytesWritten ?? 0)
                    };
                }));

            return 0;
        }

        private static int Export(NpServiceClient client, Arguments a)
        {
            var export = client.Export();
            var text = JsonConvert.SerializeObject(export, Formatting.Indented);

            var path = a.Get("output");
            if (path == null)
            {
                Console.WriteLine(text);
            }
            else
            {
                File.WriteAllText(path, text);
                Console.WriteLine($"Wrote {export.Proxies.Count} proxies and {export.Plugins.Count} plugins to {path}. The file holds certificate passwords: keep it private.");
            }

            return 0;
        }

        private static int Import(NpServiceClient client, Arguments a)
        {
            var dryRun = a.Has("dry-run");
            var export = JsonConvert.DeserializeObject<NpExport>(ReadInput(a)) ?? throw new CommandFailedException("The file is empty.");
            var failed = false;

            foreach (var plugin in export.Plugins)
            {
                failed |= ApplyPlugin(client, plugin, dryRun) == false;
            }

            foreach (var proxy in export.Proxies)
            {
                failed |= PrintApply(proxy.Name, client.ApplyProxy(proxy, dryRun), dryRun) == false;
            }

            if (export.Settings != null && dryRun == false)
            {
                client.SetSettings(export.Settings);
                Console.WriteLine("settings: applied");
            }

            return failed ? 1 : 0;
        }

        //---------------------------------------------------------------- Proxies

        private static int Proxy(NpServiceClient client, Arguments a)
        {
            var verb = a.Word(1, "proxy command (list, get, apply, validate, start, stop, restart, delete)").ToLowerInvariant();

            switch (verb)
            {
                case "list":
                    {
                        var proxies = client.ListProxies().OrderBy(o => o.Name).ToList();
                        var statistics = client.GetStatistics();

                        if (a.Has("json"))
                        {
                            Output.Json(proxies);
                            return 0;
                        }

                        Output.Table(["NAME", "STATE", "TYPE", "PORT", "HOSTS", "CONNECTIONS", "AUTOSTART"],
                            proxies.Select(o => new[]
                            {
                                o.Name, o.IsRunning ? "running" : "stopped", o.TrafficType.ToString(), $"{o.ListenPort}",
                                string.Join(",", o.HostNames.Where(h => h.Enabled).Select(h => h.Host)),
                                $"{(statistics.TryGetValue(o.Id, out var s) ? s.CurrentConnections : 0)}", o.AutoStart ? "yes" : "no"
                            }));
                        return 0;
                    }

                case "get":
                    {
                        var name = a.Word(2, "proxy name");
                        var proxy = client.GetProxy(name) ?? throw new CommandFailedException($"There is no proxy '{name}'.");
                        Console.WriteLine(JsonConvert.SerializeObject(a.Has("show-secrets") ? proxy : NpServiceClient.MaskSecrets(proxy), Formatting.Indented));
                        return 0;
                    }

                case "apply":
                case "validate":
                    {
                        var dryRun = verb == "validate" || a.Has("dry-run");
                        var failed = false;

                        foreach (var proxy in ReadProxies(ReadInput(a)))
                        {
                            failed |= PrintApply(proxy.Name, client.ApplyProxy(proxy, dryRun), dryRun) == false;
                        }

                        return failed ? 1 : 0;
                    }

                case "start":
                case "stop":
                case "restart":
                case "delete":
                    {
                        var name = a.Word(2, "proxy name");
                        var action = Enum.Parse<NpProxyAction>(verb, ignoreCase: true);
                        var problem = client.ProxyAction(name, action);

                        if (problem != null)
                        {
                            throw new CommandFailedException($"{name}: {problem}");
                        }

                        Console.WriteLine($"{name}: {verb} done");
                        return 0;
                    }

                default:
                    throw new UsageException($"Unknown proxy command '{verb}'.");
            }
        }

        private static List<NpProxyConfiguration> ReadProxies(string text)
        {
            var token = JToken.Parse(text);

            if (token is JArray array)
            {
                return array.ToObject<List<NpProxyConfiguration>>() ?? [];
            }

            if (token is JObject obj && obj["Proxies"] is JArray proxies)
            {
                return proxies.ToObject<List<NpProxyConfiguration>>() ?? [];
            }

            return [token.ToObject<NpProxyConfiguration>() ?? throw new CommandFailedException("The file does not hold a proxy.")];
        }

        private static bool PrintApply(string name, NpApplyResult result, bool dryRun)
        {
            var verb = dryRun && result.Action != "unchanged" ? $"would {result.Action}" : result.Action;
            Console.WriteLine($"proxy/{name}: {(string.IsNullOrEmpty(verb) ? "not applied" : verb)}");

            foreach (var problem in result.Problems)
            {
                Console.Error.WriteLine($"  problem: {problem}");
            }

            if (result.StartError != null)
            {
                Console.Error.WriteLine($"  saved, but it could not be started: {result.StartError}");
            }

            return result.Success && result.StartError == null;
        }

        private static int Connections(NpServiceClient client, Arguments a)
        {
            if (a.Words.Count > 1 && a.Words[1].Equals("close", StringComparison.OrdinalIgnoreCase))
            {
                var id = a.Get("id");
                var proxy = a.Words.Count > 2 ? a.Words[2] : null;

                if (id == null && proxy == null)
                {
                    throw new UsageException("Missing the proxy name, or --id.");
                }

                var closed = client.CloseConnections(id == null ? null : Guid.Parse(id), proxy);
                Console.WriteLine($"{closed} connections closed.");
                return closed > 0 ? 0 : 1;
            }

            var proxies = client.ListProxies();
            if (a.Words.Count > 1)
            {
                proxies = proxies.Where(o => o.Name.Equals(a.Words[1], StringComparison.CurrentCultureIgnoreCase)).ToList();
                if (proxies.Count == 0)
                {
                    throw new CommandFailedException($"There is no proxy '{a.Words[1]}'.");
                }
            }

            var rows = proxies.SelectMany(p => client.GetConnections(p.Id).Select(c => (Proxy: p.Name, Connection: c))).ToList();

            if (a.Has("json"))
            {
                Output.Json(rows.Select(o => new { Proxy = o.Proxy, o.Connection }));
                return 0;
            }

            Output.Table(["ID", "PROXY", "CLIENT", "DESTINATION", "PROTOCOL", "TLS", "IN", "OUT", "AGE"],
                rows.Select(o => new[]
                {
                    $"{o.Connection.Id}", o.Proxy, $"{o.Connection.SourceAddress}:{o.Connection.SourcePort}",
                    o.Connection.IsConnectedToEndpoint ? $"{o.Connection.DestinationAddress}:{o.Connection.DestinationPort}" : "-",
                    $"{o.Connection.Protocol} {o.Connection.Version}".Trim(), o.Connection.ClientTls.Length > 0 ? o.Connection.ClientTls : "-",
                    Output.Bytes(o.Connection.BytesIn), Output.Bytes(o.Connection.BytesOut),
                    $"{(DateTime.UtcNow - o.Connection.StartDateTime):hh\\:mm\\:ss}"
                }));
            return 0;
        }

        //---------------------------------------------------------------- Users

        private static int Users(NpServiceClient client, Arguments a)
        {
            var verb = a.Word(1, "user command (list, add, passwd, remove)").ToLowerInvariant();

            switch (verb)
            {
                case "list":
                    {
                        var users = client.ListUsers();
                        if (a.Has("json"))
                        {
                            Output.Json(users.Select(o => new { o.UserName, o.Description }));
                            return 0;
                        }

                        Output.Table(["USER", "DESCRIPTION"], users.OrderBy(o => o.UserName).Select(o => new[] { o.UserName, o.Description }));
                        return 0;
                    }

                case "add":
                case "passwd":
                    {
                        var name = a.Word(2, "user name");
                        var password = a.Get("password") ?? (a.Has("password-stdin") ? Input.ReadLine() ?? string.Empty : AskNewPassword(name));

                        var problem = verb == "add"
                            ? client.AddUser(name, password, a.Get("description") ?? string.Empty)
                            : client.SetPassword(name, password);

                        return Done(problem, verb == "add" ? $"user {name}: added" : $"user {name}: password set");
                    }

                case "remove":
                    {
                        var name = a.Word(2, "user name");
                        return Done(client.RemoveUser(name), $"user {name}: removed");
                    }

                default:
                    throw new UsageException($"Unknown user command '{verb}'.");
            }
        }

        private static string AskNewPassword(string user)
        {
            var first = ReadPassword($"New password for {user}: ");
            if (Console.IsInputRedirected == false && ReadPassword("Again: ") != first)
            {
                throw new CommandFailedException("The passwords are not the same.");
            }

            return first;
        }

        private static int Done(string? problem, string success)
        {
            if (problem != null)
            {
                throw new CommandFailedException(problem);
            }

            Console.WriteLine(success);
            return 0;
        }

        //---------------------------------------------------------------- Plugins

        private static int Plugins(NpServiceClient client, Arguments a)
        {
            var verb = a.Word(1, "plugin command (list, apply, remove, reload, up, down)").ToLowerInvariant();

            switch (verb)
            {
                case "list":
                    {
                        var plugins = client.ListPlugins();
                        if (a.Has("json"))
                        {
                            Output.Json(plugins);
                            return 0;
                        }

                        Output.Table(["#", "NAME", "ENABLED", "STATUS", "PLUGIN", "VERSION"],
                            plugins.Select((o, i) => new[]
                            {
                                $"{i + 1}", o.Name, o.Enabled ? "yes" : "no",
                                o.Enabled == false ? "disabled" : (o.IsLoaded ? "loaded" : $"not loaded: {o.LoadError}"), o.PluginName, o.PluginVersion
                            }));
                        return 0;
                    }

                case "apply":
                    {
                        var token = JToken.Parse(ReadInput(a));
                        var plugins = token is JArray array ? array.ToObject<List<NpPluginConfiguration>>() ?? []
                            : (token is JObject obj && obj["Plugins"] is JArray inner ? inner.ToObject<List<NpPluginConfiguration>>() ?? []
                            : [token.ToObject<NpPluginConfiguration>()!]);

                        var failed = false;
                        foreach (var plugin in plugins)
                        {
                            failed |= ApplyPlugin(client, plugin, dryRun: false) == false;
                        }

                        return failed ? 1 : 0;
                    }

                case "remove":
                case "reload":
                case "up":
                case "down":
                    {
                        var name = a.Word(2, "plugin name");
                        var action = verb switch
                        {
                            "remove" => NpPluginAction.Delete,
                            "reload" => NpPluginAction.Reload,
                            "up" => NpPluginAction.MoveUp,
                            _ => NpPluginAction.MoveDown
                        };

                        return Done(client.PluginAction(name, action), $"plugin {name}: {verb} done");
                    }

                default:
                    throw new UsageException($"Unknown plugin command '{verb}'.");
            }
        }

        private static bool ApplyPlugin(NpServiceClient client, NpPluginConfiguration plugin, bool dryRun)
        {
            if (dryRun)
            {
                Console.WriteLine($"plugin/{plugin.Name}: would apply");
                return true;
            }

            var reply = client.ApplyPlugin(plugin);
            Console.WriteLine($"plugin/{plugin.Name}: {(reply.Success ? reply.Action : "not applied")}");

            if (reply.Message != null)
            {
                Console.Error.WriteLine($"  problem: {reply.Message}");
            }

            if (string.IsNullOrEmpty(reply.LoadError) == false)
            {
                Console.Error.WriteLine($"  saved, but it could not be loaded: {reply.LoadError}");
            }

            return reply.Success && string.IsNullOrEmpty(reply.LoadError);
        }

        //---------------------------------------------------------------- Settings

        private static int Settings(NpServiceClient client, Arguments a)
        {
            var verb = a.Word(1, "settings command (get, set)").ToLowerInvariant();

            if (verb == "get")
            {
                Console.WriteLine(JsonConvert.SerializeObject(client.GetSettings(), Formatting.Indented));
                return 0;
            }

            if (verb == "set")
            {
                var settings = JsonConvert.DeserializeObject<NpServiceSettings>(ReadInput(a)) ?? throw new CommandFailedException("The file is empty.");
                Console.WriteLine(JsonConvert.SerializeObject(client.SetSettings(settings), Formatting.Indented));
                return 0;
            }

            throw new UsageException($"Unknown settings command '{verb}'.");
        }

        //---------------------------------------------------------------- Input

        /// <summary>
        /// The text of the file given with -f, or of the standard input for "-".
        /// </summary>
        private static string ReadInput(Arguments a)
        {
            var path = a.Get("file") ?? throw new UsageException("Missing -f <file> (- for the standard input).");

            try
            {
                return path == "-" ? Input.ReadAll() : File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new CommandFailedException($"The file '{path}' could not be read: {ex.Message}");
            }
        }
    }
}
