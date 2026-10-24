namespace NetProxy.Cli
{
    /// <summary>
    /// The command line: words, and options (--name value, --flag, -f value).
    /// </summary>
    internal sealed class Arguments
    {
        /// <summary>The options that take a value. Anything else that starts with a dash is a flag.</summary>
        private static readonly HashSet<string> _valueOptions = new(StringComparer.OrdinalIgnoreCase)
        {
            "server", "port", "user", "password", "f", "file", "o", "output", "n", "lines", "id", "description", "proxy"
        };

        private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["f"] = "file", ["o"] = "output", ["n"] = "lines", ["h"] = "help", ["s"] = "server", ["u"] = "user"
        };

        private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Words { get; } = [];

        public Arguments(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg == "-" || arg.StartsWith('-') == false)
                {
                    Words.Add(arg);
                    continue;
                }

                var name = arg.TrimStart('-');
                var inline = name.IndexOf('=');
                string? value = null;
                if (inline > 0)
                {
                    value = name[(inline + 1)..];
                    name = name[..inline];
                }

                if (_aliases.TryGetValue(name, out var full))
                {
                    name = full;
                }

                if (_valueOptions.Contains(name))
                {
                    if (value == null)
                    {
                        if (i + 1 >= args.Length)
                        {
                            throw new UsageException($"The option --{name} needs a value.");
                        }

                        value = args[++i];
                    }

                    _options[name] = value;
                }
                else
                {
                    _flags.Add(name);
                }
            }
        }

        public bool Has(string flag) => _flags.Contains(flag);

        public string? Get(string option) => _options.TryGetValue(option, out var value) ? value : null;

        /// <summary>
        /// The word at this position, or a usage error that says what is missing.
        /// </summary>
        public string Word(int index, string what)
            => index < Words.Count ? Words[index] : throw new UsageException($"Missing {what}.");
    }

    /// <summary>
    /// The command line is wrong. The message says how.
    /// </summary>
    internal sealed class UsageException(string message) : Exception(message);

    /// <summary>
    /// The service did not do what was asked, or could not be reached. The message is for the user.
    /// </summary>
    internal sealed class CommandFailedException(string message, int exitCode = 1) : Exception(message)
    {
        public int ExitCode { get; } = exitCode;
    }
}
