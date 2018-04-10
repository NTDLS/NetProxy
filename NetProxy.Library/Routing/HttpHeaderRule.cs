using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NetProxy.Library.Routing
{
    public class HttpHeaderRule
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public HttpHeaderType HeaderType { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public HttpHeaderAction Action { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public HTTPVerb Verb { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }

        public HttpHeaderRule(HttpHeaderType headerType, HTTPVerb verb, string name, HttpHeaderAction action, string value)
        {
            HeaderType = headerType;
            Verb = verb;
            Name = name;
            Action = action;
            Value = value;
        }

        public HttpHeaderRule()
        {

        }
    }
}
