using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using NetProxy.Service.Proxy;
using System.Net;

namespace NetProxy.Service
{
    /// <summary>
    /// The rules that a proxy configuration has to follow, wherever it comes from: the console, the command line tool, or the files.
    /// </summary>
    internal static class NpProxyValidator
    {
        /// <summary>
        /// Cleans up the host names of the proxy (normalized) and returns what is wrong with it, if anything. <paramref name="others"/>
        /// are the other proxies of the service: the name has to be unique and the port can only be shared the way the rules say.
        /// </summary>
        public static List<string> Validate(NpProxyConfiguration configuration, IEnumerable<NpProxyConfiguration> others,
            IReadOnlyCollection<string>? certificateNames = null)
        {
            var problems = new List<string>();
            var label = string.IsNullOrWhiteSpace(configuration.Name) ? "(no name)" : configuration.Name;

            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                problems.Add("The proxy needs a name.");
            }

            if (configuration.ListenPort is < 1 or > 65535)
            {
                problems.Add($"Proxy '{label}': the listen port must be between 1 and 65535.");
            }

            if (configuration.Endpoints.Collection.Count == 0)
            {
                problems.Add($"Proxy '{label}': it needs at least one endpoint.");
            }

            foreach (var endpoint in configuration.Endpoints.Collection)
            {
                if (string.IsNullOrWhiteSpace(endpoint.Address) || endpoint.Port is < 1 or > 65535)
                {
                    problems.Add($"Proxy '{label}': the endpoint '{endpoint.Address}:{endpoint.Port}' needs an address and a port between 1 and 65535.");
                }
            }

            if (configuration.ListenOnAllAddresses == false)
            {
                foreach (var binding in configuration.Bindings.Where(o => o.Enabled))
                {
                    var address = binding.Address.Trim();
                    var unbracketed = address.StartsWith('[') && address.EndsWith(']') ? address[1..^1] : address;
                    if (IPAddress.TryParse(unbracketed, out _) == false)
                    {
                        problems.Add($"Proxy '{label}': '{binding.Address}' is not a valid IPv4 or IPv6 address.");
                    }
                }

                if (configuration.Bindings.Any(o => o.Enabled) == false)
                {
                    problems.Add($"Proxy '{label}': it needs a binding, or to listen on all addresses.");
                }
            }

            if (configuration.TrafficType == TrafficType.Udp)
            {
                //A datagram has no host name, no TLS, and no HTTP header: what is only for those would silently do nothing.
                if (configuration.HostNames.Any(o => string.IsNullOrWhiteSpace(o.Host) == false))
                {
                    problems.Add($"Proxy '{label}': a UDP proxy cannot have host names (a datagram does not say which host it is for).");
                }

                if (string.IsNullOrWhiteSpace(configuration.TlsCertificate) == false)
                {
                    problems.Add($"Proxy '{label}': a UDP proxy cannot have a TLS certificate.");
                }

                if (configuration.ForwardedHeaders != ForwardedHeadersMode.Off)
                {
                    problems.Add($"Proxy '{label}': a UDP proxy cannot add forwarded headers.");
                }

                if (configuration.HttpHeaderRules.Collection.Count > 0)
                {
                    problems.Add($"Proxy '{label}': a UDP proxy cannot have HTTP header rules.");
                }
            }

            if (configuration.MaxDatagramsPerSecondPerClient < 0)
            {
                problems.Add($"Proxy '{label}': the most datagrams per second cannot be negative (0 is no limit).");
            }

            var hostProblem = NpHostRouter.NormalizeAndValidate(configuration);
            if (hostProblem != null)
            {
                problems.Add($"Proxy '{label}': {hostProblem}");
            }

            //A proxy refers to its certificates by name, so the names have to be certificates that are set up for the service.
            var referenced = new List<string> { configuration.TlsCertificate };
            referenced.AddRange(configuration.HostNames.Select(o => o.TlsCertificate));
            foreach (var certificate in referenced.Where(o => string.IsNullOrWhiteSpace(o) == false).Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var known = certificateNames != null
                    ? certificateNames.Any(o => o.Equals(certificate, StringComparison.OrdinalIgnoreCase))
                    : Singletons.Certificates.Exists(certificate);

                if (known == false)
                {
                    problems.Add($"Proxy '{label}': the TLS certificate '{certificate}' is not set up for the service (add it under Settings, Certificates).");
                }
            }

            var otherList = others.Where(o => o.Id != configuration.Id).ToList();

            if (string.IsNullOrWhiteSpace(configuration.Name) == false
                && otherList.Any(o => o.Name.Equals(configuration.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                problems.Add($"There is already a proxy named '{configuration.Name}'.");
            }

            if (hostProblem == null)
            {
                var conflict = NpHostRouter.FindSharingConflict(configuration, otherList);
                if (conflict != null)
                {
                    problems.Add($"Proxy '{label}': {conflict}");
                }
            }

            return problems;
        }
    }
}
