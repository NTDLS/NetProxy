using NetProxy.Library.Payloads.Routing;
using NetProxy.Library.Utilities;
using System.Security.Cryptography.X509Certificates;

namespace NetProxy.Service.Proxy
{
    /// <summary>
    /// Picks the TLS certificate of a proxy: the one of the proxy and the ones of its host names. The certificates themselves belong
    /// to <see cref="NpCertificateManager"/>, which reads and watches the files, so a certificate that is renewed is used for new
    /// connections without the proxy being restarted. The connections that are open keep the certificate they started with.
    /// </summary>
    internal sealed class NpCertificateStore
    {
        private readonly NpProxyConfiguration _configuration;

        private NpCertificateStore(NpProxyConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// The certificate of the proxy, if it has one.
        /// </summary>
        public X509Certificate2? Default => Singletons.Certificates.Get(_configuration.TlsCertificate);

        /// <summary>
        /// The certificate to present to a client that asks for a host name: the one of the most specific host name that matches and
        /// has its own, otherwise the certificate of the proxy. Null if there is none.
        /// </summary>
        public X509Certificate2? Get(string? host)
        {
            var normalized = NpHostMatcher.Normalize(host);

            if (normalized != null)
            {
                var names = new Dictionary<string, string>();
                foreach (var hostName in _configuration.HostNames.Where(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificate) == false))
                {
                    var pattern = NpHostMatcher.Normalize(hostName.Host);
                    if (pattern != null)
                    {
                        names[pattern] = hostName.TlsCertificate;
                    }
                }

                if (names.Count > 0)
                {
                    var pattern = NpHostMatcher.FindBest(names.Keys, normalized);
                    if (pattern != null)
                    {
                        return Singletons.Certificates.Get(names[pattern]) ?? Default;
                    }
                }
            }

            return Default;
        }

        /// <summary>
        /// Checks that the certificates of the proxy can be used, and returns null if it has none. Throws (with the reason) if one cannot.
        /// </summary>
        public static NpCertificateStore? Create(NpProxyConfiguration configuration)
        {
            var names = new List<string>();

            if (string.IsNullOrWhiteSpace(configuration.TlsCertificate) == false)
            {
                names.Add(configuration.TlsCertificate);
            }

            names.AddRange(configuration.HostNames.Where(o => o.Enabled && string.IsNullOrWhiteSpace(o.TlsCertificate) == false).Select(o => o.TlsCertificate));

            foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var problem = Singletons.Certificates.GetProblem(name);
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }
            }

            return names.Count == 0 ? null : new NpCertificateStore(configuration);
        }
    }
}
