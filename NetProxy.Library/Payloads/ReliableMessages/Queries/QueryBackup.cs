using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Everything that the service is set up with, as one document (<see cref="NpConfigurationBackup"/> as JSON), to be kept as a backup.
    /// </summary>
    public class QueryExportConfiguration : IRmQuery<QueryExportConfigurationReply>
    {
    }

    public class QueryExportConfigurationReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public string Document { get; set; } = string.Empty;
    }

    /// <summary>
    /// Replaces everything that the service is set up with by what the document holds: the document is checked first, and if it is
    /// not good nothing is changed. Otherwise the proxies are stopped, the current configuration is replaced, and the proxies that
    /// are set to start automatically are started. A copy of what was there is kept in the Backups folder of the service.
    /// </summary>
    public class QueryImportConfiguration : IRmQuery<QueryImportConfigurationReply>
    {
        public string Document { get; set; } = string.Empty;

        /// <summary>Only check the document, and say what it holds.</summary>
        public bool DryRun { get; set; }
    }

    public class QueryImportConfigurationReply : IRmQueryReply
    {
        public string? Message { get; set; }

        /// <summary>The configuration was replaced (or, for a dry run, it can be).</summary>
        public bool Success { get; set; }

        /// <summary>What is wrong with the document or with the proxies in it. Nothing was changed when there is any, except for the
        /// problems that are about starting a proxy (a port in use, for example): the configuration is in place then.</summary>
        public List<string> Problems { get; set; } = new();

        public int Proxies { get; set; }
        public int Certificates { get; set; }
        public int Plugins { get; set; }
        public int Users { get; set; }

        /// <summary>Where the copy of the previous configuration was written.</summary>
        public string? PreviousConfigurationFile { get; set; }
    }
}
