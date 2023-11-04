using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Routing
{
    public class Endpoints
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionPattern ConnectionPattern { get; set; }

        public List<Endpoint> Collection = new();

        public void Add(Endpoint peer)
        {
            Collection.Add(peer);
        }
    }
}
