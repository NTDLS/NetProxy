namespace NetProxy.Library.Routing
{
    public class NpHTTPHeaderRules
    {
        public List<NpHttpHeaderRule> Collection = new();

        public void Add(NpHttpHeaderRule rule)
        {
            Collection.Add(rule);
        }
    }
}