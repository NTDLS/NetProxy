using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Re-reads the configuration files and applies what changed in them: proxies that were added, changed or removed (the others
    /// keep running and keep their connections), the users, the settings and the plugins. With <see cref="DryRun"/> it only checks
    /// the files and says what it would do.
    /// </summary>
    public class QueryReloadConfiguration : IRmQuery<QueryReloadConfigurationReply>
    {
        public bool DryRun { get; set; }
    }

    public class QueryReloadConfigurationReply : IRmQueryReply
    {
        public string? Message { get; set; }

        /// <summary>False if the files have a problem, in which case nothing was changed.</summary>
        public bool Success { get; set; }

        public List<string> Changes { get; set; } = new();
        public List<string> Problems { get; set; } = new();
    }
}
