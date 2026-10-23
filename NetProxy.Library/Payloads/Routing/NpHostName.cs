namespace NetProxy.Library.Payloads.Routing
{
    /// <summary>
    /// A host name that a proxy answers to, so that several proxies can share a listen port. A name is matched against the Host
    /// header of the request (HTTP) or the server name that the client asks for (HTTPS). It is an exact name ("www.example.com")
    /// or a wildcard for the names below a domain ("*.example.com", which does not include "example.com" itself).
    /// </summary>
    public class NpHostName
    {
        public bool Enabled { get; set; } = true;

        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Path (on the machine running the service) of the PFX/PKCS#12 certificate that is presented to clients that ask for this
        /// host name (Https traffic type). When it is empty the certificate of the proxy is used.
        /// </summary>
        public string TlsCertificatePath { get; set; } = string.Empty;

        /// <summary>
        /// Password of the PFX certificate, if it has one. Stored encrypted by the service.
        /// </summary>
        public string TlsCertificatePassword { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
