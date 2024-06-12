using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Payloads.Routing
{
    public class NpEndpoints
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionPattern ConnectionPattern { get; set; }

        public List<NpEndpoint> Collection = new();

        public void Add(NpEndpoint peer)
        {
            Collection.Add(peer);
        }
    }
}
