using MaxMind.Db;
using System.Net;

namespace NetProxy.Plugins.GeoFilter
{
    /// <summary>
    /// Says where an address is: the country and the network (autonomous system) it belongs to.
    /// </summary>
    internal interface IGeoLookup
    {
        /// <summary>The ISO code of the country ("US", "DE") in upper case, or null when it is not known.</summary>
        string? Country(IPAddress address);

        /// <summary>The number of the autonomous system, or null when it is not known.</summary>
        long? Asn(IPAddress address);
    }

    /// <summary>
    /// The databases in the MaxMind DB format (.mmdb): GeoLite2-Country and GeoLite2-ASN from MaxMind, or the free country and ASN
    /// databases of DB-IP, which have the same fields. The files are read into memory, so that they can be replaced while the
    /// service runs (the plug-in notices, and loads the new one).
    /// </summary>
    internal sealed class MaxMindLookup : IGeoLookup, IDisposable
    {
        private readonly Reader? _country;
        private readonly Reader? _asn;

        public MaxMindLookup(string? countryFile, string? asnFile)
        {
            _country = countryFile == null ? null : new Reader(countryFile, FileAccessMode.Memory);
            _asn = asnFile == null ? null : new Reader(asnFile, FileAccessMode.Memory);
        }

        public string? Country(IPAddress address)
        {
            if (_country?.Find<Dictionary<string, object>>(address) is not { } record)
            {
                return null;
            }

            //The country the address is in, or else the one it is registered in (an address with no location, a satellite provider).
            foreach (var key in new[] { "country", "registered_country" })
            {
                if (record.TryGetValue(key, out var value) && value is Dictionary<string, object> country
                    && country.TryGetValue("iso_code", out var code) && code is string text && text.Length == 2)
                {
                    return text.ToUpperInvariant();
                }
            }

            return null;
        }

        public long? Asn(IPAddress address)
        {
            if (_asn?.Find<Dictionary<string, object>>(address) is { } record && record.TryGetValue("autonomous_system_number", out var number))
            {
                return Convert.ToInt64(number, System.Globalization.CultureInfo.InvariantCulture);
            }

            return null;
        }

        public void Dispose()
        {
            _country?.Dispose();
            _asn?.Dispose();
        }
    }
}
