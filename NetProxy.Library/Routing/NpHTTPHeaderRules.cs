namespace NetProxy.Library.Routing
{
    public class NpHTTPHeaderRules
    {
        public List<NpHttpHeaderRule> List = new();

        public void Add(NpHttpHeaderRule rule)
        {
            List.Add(rule);
        }
    }
}