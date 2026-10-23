using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// The TLS certificates that the proxies are set up with, and when they expire.
    /// </summary>
    public class QueryCertificateStatus : IRmQuery<QueryCertificateStatusReply>
    {
    }

    public class NpCertificateStatus
    {
        public string ProxyName { get; set; } = string.Empty;

        /// <summary>The host name the certificate is for, or empty for the certificate of the proxy.</summary>
        public string HostName { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime? NotAfterUtc { get; set; }
        public int? DaysLeft { get; set; }

        /// <summary>Why the certificate could not be read.</summary>
        public string Error { get; set; } = string.Empty;
    }

    public class QueryCertificateStatusReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<NpCertificateStatus> Collection { get; set; } = new();
    }
}
