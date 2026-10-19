namespace NetProxy.Library.Payloads.Routing
{
    public class NpHTTPHeaderRules
    {
        public List<NpHttpHeaderRule> Collection { get; set; } = new();

        public void Add(NpHttpHeaderRule rule)
        {
            Collection.Add(rule);
        }
    }
}