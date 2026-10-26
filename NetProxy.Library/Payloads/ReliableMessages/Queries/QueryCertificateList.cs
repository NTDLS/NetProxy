using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// The TLS certificates of the service, what they hold, when they expire and which proxies use them.
    /// </summary>
    public class QueryCertificateList : IRmQuery<QueryCertificateListReply>
    {
    }

    public class QueryCertificateListReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<NpCertificateGridItem> Collection { get; set; } = new();
    }

    /// <summary>
    /// Adds or replaces a certificate and says how it went. With <see cref="MatchByName"/> the certificate with the same name is the one
    /// replaced. Renaming a certificate renames it in the proxies that use it.
    /// </summary>
    public class QueryUpsertCertificate : IRmQuery<QueryUpsertCertificateReply>
    {
        public NpCertificateConfiguration Certificate { get; set; } = new();
        public bool MatchByName { get; set; }
    }

    public class QueryUpsertCertificateReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public bool Success { get; set; }
        public string Action { get; set; } = string.Empty;

        /// <summary>Why the file cannot be used, when the certificate was saved but could not be loaded.</summary>
        public string? LoadError { get; set; }
    }

    public enum NpCertificateAction { Delete }

    /// <summary>
    /// Deletes a certificate, found by name or by id. A certificate that proxies use is not deleted.
    /// </summary>
    public class QueryCertificateAction : IRmQuery<QueryActionReply>
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public NpCertificateAction Action { get; set; }
    }
}
