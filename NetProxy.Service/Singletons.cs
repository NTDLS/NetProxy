using NetProxy.Library.Logging;
using NetProxy.Library.Payloads;
using NetProxy.Library.Utilities;
using NetProxy.Service.Logging;

namespace NetProxy.Service
{
    public static class Singletons
    {
        /// <summary>
        /// The name that the configuration files are stored under (the folder in the common application data folder). It is the
        /// product name unless the NETPROXY_CONFIG_NAME environment variable says otherwise, which lets a test or development
        /// instance keep its own configuration without touching the real one.
        /// </summary>
        public static string ConfigurationName { get; } =
            Environment.GetEnvironmentVariable("NETPROXY_CONFIG_NAME") is { Length: > 0 } name ? name : NetProxy.Library.Constants.FriendlyName;

        /// <summary>
        /// The plug-ins of the service.
        /// </summary>
        internal static NetProxy.Service.Plugins.NpPluginManager Plugins { get; } = new();

        /// <summary>
        /// The TLS certificates of the service, which the proxies refer to by name.
        /// </summary>
        internal static NetProxy.Service.Proxy.NpCertificateManager Certificates { get; } = new();

        /// <summary>
        /// The statistics of the proxies, which are kept across restarts.
        /// </summary>
        internal static NetProxy.Service.Proxy.NpStatisticsStore Statistics { get; } = new();

        private static readonly object _loggingLock = new();
        private static NpLogging? _logging;
        private static RollingFileLogSink? _fileSink;
        private static EventLogSink? _eventLogSink;

        /// <summary>
        /// The logger. It writes to the console (when running interactively), to a rolling file and to the Windows event log.
        /// Until the configuration has been loaded it uses the default settings; see <see cref="ConfigureLogging"/>.
        /// </summary>
        public static NpLogging Logging
        {
            get
            {
                lock (_loggingLock)
                {
                    return _logging ??= CreateLogging(new NpServiceSettings());
                }
            }
        }

        private static NpLogging CreateLogging(NpServiceSettings settings)
        {
            var sinks = new List<ILogSink>();

            if (Environment.UserInteractive)
            {
                //A service has no console, so this is only for running the service by hand.
                sinks.Add(new ConsoleLogSink());
            }

            _fileSink = new RollingFileLogSink(settings.LogPath, settings.LogFileMaxSizeMB * 1024L * 1024L, settings.LogFileRetainedCount);
            sinks.Add(_fileSink);

            if (OperatingSystem.IsWindows())
            {
                _eventLogSink = new EventLogSink { IsEnabled = settings.LogToEventLog };
                sinks.Add(_eventLogSink);
            }

            return new NpLogging(settings.LogLevel, [.. sinks]);
        }

        /// <summary>
        /// Applies the logging settings, which takes effect immediately without restarting the service.
        /// </summary>
        public static void ConfigureLogging(NpServiceSettings settings)
        {
            var logging = Logging;

            lock (_loggingLock)
            {
                logging.Level = settings.LogLevel;

                _fileSink?.Configure(settings.LogPath, settings.LogFileMaxSizeMB * 1024L * 1024L, settings.LogFileRetainedCount);

                if (OperatingSystem.IsWindows() && _eventLogSink != null)
                {
                    _eventLogSink.IsEnabled = settings.LogToEventLog;
                }
            }
        }

        /// <summary>
        /// Writes out whatever is still queued. Call when the service stops.
        /// </summary>
        public static void ShutdownLogging()
        {
            lock (_loggingLock)
            {
                _logging?.Dispose();
                _logging = null;
            }
        }
    }
}
