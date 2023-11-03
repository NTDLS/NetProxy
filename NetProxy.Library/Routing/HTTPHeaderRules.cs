namespace NetProxy.Library.Routing
{
    public class HttpHeaderRules
    {
        public List<HttpHeaderRule> List = new();

        public void Add(HttpHeaderRule rule)
        {
            List.Add(rule);
        }
    }
}