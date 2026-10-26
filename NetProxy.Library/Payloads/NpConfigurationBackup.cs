using NetProxy.Library.Payloads.Routing;

namespace NetProxy.Library.Payloads
{
    /// <summary>
    /// Everything the service is set up with, in one document: what the console exports as a backup and imports to put a configuration
    /// in place of the current one. It holds the passwords of the certificates and the password hashes of the users, so it is to be
    /// kept private.
    /// </summary>
    public class NpConfigurationBackup
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>The version of the service that wrote the document.</summary>
        public string ServiceVersion { get; set; } = string.Empty;

        /// <summary>The port of the management interface. A different one takes effect when the service restarts.</summary>
        public int ManagementPort { get; set; }

        public NpServiceSettings Settings { get; set; } = new();
        public List<NpUser> Users { get; set; } = new();
        public List<NpCertificateConfiguration> Certificates { get; set; } = new();
        public List<NpPluginConfiguration> Plugins { get; set; } = new();
        public List<NpProxyConfiguration> Proxies { get; set; } = new();
    }
}
