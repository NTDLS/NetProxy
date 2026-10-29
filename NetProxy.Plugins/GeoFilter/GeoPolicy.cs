using NetProxy.Plugins.Common;
using System.Net;

namespace NetProxy.Plugins.GeoFilter
{
    /// <summary>
    /// Who is let in, by where they are: country and network. An address that is allowed (Allow, and private addresses) is always let in,
    /// then a country or a network that is denied is not, then if there is a list of countries that are allowed only they are. An
    /// address that can not be placed is let in, unless AllowUnknown says it is not.
    /// </summary>
    internal sealed class GeoPolicy
    {
        public HashSet<string> AllowCountries { get; init; } = [];
        public HashSet<string> DenyCountries { get; init; } = [];
        public HashSet<long> DenyAsns { get; init; } = [];
        public HashSet<long> AllowAsns { get; init; } = [];
        public bool AllowUnknown { get; init; } = true;
        public IReadOnlyList<AddressRange> Allow { get; init; } = [];
        public bool AllowPrivate { get; init; } = true;

        public bool HasRules => AllowCountries.Count + DenyCountries.Count + DenyAsns.Count > 0;

        /// <summary>
        /// Null when the address is let in, and otherwise why it is not.
        /// </summary>
        public string? Check(IPAddress address, IGeoLookup? lookup, string? countryHint = null)
        {
            if ((AllowPrivate && AddressRange.Private.Any(o => o.Contains(address))) || Allow.Any(o => o.Contains(address)))
            {
                return null;
            }

            var country = lookup?.Country(address) ?? countryHint;
            var asn = DenyAsns.Count + AllowAsns.Count > 0 ? lookup?.Asn(address) : null;

            if (asn is { } number && AllowAsns.Contains(number))
            {
                return null;
            }

            if (asn is { } denied && DenyAsns.Contains(denied))
            {
                return $"network AS{denied}";
            }

            if (country != null && DenyCountries.Contains(country))
            {
                return $"country {country}";
            }

            if (AllowCountries.Count > 0)
            {
                if (country == null)
                {
                    return AllowUnknown ? null : "an unknown country";
                }

                return AllowCountries.Contains(country) ? null : $"country {country}";
            }

            return country == null && lookup != null && AllowUnknown == false && DenyCountries.Count > 0 ? "an unknown country" : null;
        }
    }
}
