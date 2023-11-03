using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NetProxy.Library.Routing
{
    public class Endpoints
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ConnectionPattern ConnectionPattern { get; set; }

        public List<Endpoint> List = new List<Endpoint>();

        public void Add(Endpoint peer)
        {
            List.Add(peer);
        }
    }
}
