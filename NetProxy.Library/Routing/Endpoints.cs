using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Routing
{
    public class Endpoints
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionPattern ConnectionPattern { get; set; }

        public List<Endpoint> List = new();

        public void Add(Endpoint peer)
        {
            List.Add(peer);
        }
    }
}
