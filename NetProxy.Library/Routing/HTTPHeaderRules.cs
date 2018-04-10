using System.Collections.Generic;

namespace NetProxy.Library.Routing
{
    public class HttpHeaderRules
    {
        public List<HttpHeaderRule> List = new List<HttpHeaderRule>();

        public void Add(HttpHeaderRule rule)
        {
            List.Add(rule);
        }
    }
}