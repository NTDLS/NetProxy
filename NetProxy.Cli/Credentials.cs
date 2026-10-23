using Newtonsoft.Json;
using NetProxy.ClientApi;
using NetProxy.Library;

namespace NetProxy.Cli
{
    /// <summary>
    /// Where the tool connects, and who it logs in as. The login is kept per user, in the profile of the user that ran
    /// <c>netproxyctl login</c> (the file is only readable by that user on Linux and macOS), and holds the hash of the password,
    /// not the password. Environment variables and options override it.
    /// </summary>
    internal sealed class Credentials
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = Constants.DefaultManagementPort;
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Where the login is kept: in the profile of the user, unless NETPROXY_CREDENTIALS names another file.
        /// </summary>
        public static string FilePath
            => Environment.GetEnvironmentVariable("NETPROXY_CREDENTIALS") is { Length: > 0 } path
                ? path
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netproxy", "credentials.json");

        public static Credentials? Load()
        {
            try
            {
                return File.Exists(FilePath) ? JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(FilePath)) : null;
            }
            catch
            {
                return null;
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            //Created closed to everyone but the user before anything is written to it.
            if (File.Exists(FilePath) == false)
            {
                File.WriteAllText(FilePath, string.Empty);
                if (OperatingSystem.IsWindows() == false)
                {
                    File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// What to connect with: the options of the command line, then the environment, then the saved login.
        /// </summary>
        public static NpConnectionOptions Resolve(Arguments arguments)
        {
            var saved = Load();

            var server = arguments.Get("server") ?? Environment.GetEnvironmentVariable("NETPROXY_SERVER") ?? saved?.Server;
            var user = arguments.Get("user") ?? Environment.GetEnvironmentVariable("NETPROXY_USER") ?? saved?.UserName;
            var portText = arguments.Get("port") ?? Environment.GetEnvironmentVariable("NETPROXY_PORT");
            var password = arguments.Get("password") ?? Environment.GetEnvironmentVariable("NETPROXY_PASSWORD");

            if (string.IsNullOrWhiteSpace(server))
            {
                server = "localhost";
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                throw new CommandFailedException("Not logged in. Run 'netproxyctl login --server <host> --user <name>', or set NETPROXY_USER and NETPROXY_PASSWORD.", 3);
            }

            if (int.TryParse(portText, out var port) == false)
            {
                port = saved?.Port ?? Constants.DefaultManagementPort;
            }

            //A password given another way is for the user it was given for, not the saved one.
            var sameUser = saved != null && string.Equals(saved.UserName, user, StringComparison.OrdinalIgnoreCase);
            var hash = password != null ? NpConnectionOptions.HashPassword(password) : (sameUser ? saved!.PasswordHash : string.Empty);

            if (hash.Length == 0)
            {
                throw new CommandFailedException($"No password for '{user}'. Run 'netproxyctl login', or set NETPROXY_PASSWORD.", 3);
            }

            return new NpConnectionOptions { Server = server, Port = port, UserName = user, PasswordHash = hash };
        }
    }
}
