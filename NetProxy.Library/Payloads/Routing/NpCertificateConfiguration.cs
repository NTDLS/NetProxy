namespace NetProxy.Library.Payloads.Routing
{
    /// <summary>
    /// A TLS certificate that the service uses. Certificates are set up once for the service and given a name; proxies and their
    /// host names then refer to them by that name (<see cref="NpProxyConfiguration.TlsCertificate"/>). The file is watched, so
    /// a renewed certificate is used by every proxy that refers to it without anything being restarted.
    /// </summary>
    public class NpCertificateConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// What proxies call this certificate. Not case sensitive.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The PFX/PKCS#12 file, on the machine that the service runs on. A relative path is relative to the folder of the service.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The password of the file, if it has one. Stored encrypted by the service.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// A certificate as the console lists it: its configuration, what the file holds and which proxies use it.
    /// </summary>
    public class NpCertificateGridItem : NpCertificateConfiguration
    {
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Why the file could not be used, when it could not.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
        public DateTime? NotAfterUtc { get; set; }
        public int? DaysLeft { get; set; }

        /// <summary>
        /// The names of the proxies that use the certificate, for the proxy itself or for one of its host names.
        /// </summary>
        public List<string> UsedBy { get; set; } = new();
    }
}
