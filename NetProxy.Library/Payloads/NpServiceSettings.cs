using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NetProxy.Library.Utilities;

namespace NetProxy.Library.Payloads
{
    /// <summary>
    /// Settings of the service itself (not of a proxy).
    /// </summary>
    public class NpServiceSettings
    {
        /// <summary>
        /// The lowest severity that is logged. Takes effect immediately.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public NpLogging.Severity LogLevel { get; set; } = NpLogging.Severity.Information;

        /// <summary>
        /// The file to log to, on the machine running the service. Relative paths are relative to the service's installation
        /// folder. Empty turns file logging off.
        /// </summary>
        public string LogPath { get; set; } = "Logs/NetProxy.log";

        /// <summary>
        /// The log file is rolled over (NetProxy.log, NetProxy.1.log...) when it reaches this size.
        /// </summary>
        public int LogFileMaxSizeMB { get; set; } = Constants.DefaultLogFileMaxSizeMB;

        /// <summary>
        /// The number of rolled over log files to keep, in addition to the current one.
        /// </summary>
        public int LogFileRetainedCount { get; set; } = Constants.DefaultLogFileRetainedCount;

        /// <summary>
        /// Also write warnings and exceptions to the Windows Application event log.
        /// </summary>
        public bool LogToEventLog { get; set; } = true;

        /// <summary>
        /// The management port accepts IPv6 connections as well as IPv4 (one dual-stack listener). Applies when the service starts.
        /// </summary>
        public bool ManagementListenOnIPv6 { get; set; } = true;
    }
}
