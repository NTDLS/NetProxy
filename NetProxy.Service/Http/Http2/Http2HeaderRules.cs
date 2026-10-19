using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using System.Text;

namespace NetProxy.Service.Http.Http2
{
    /// <summary>
    /// Applies HTTP header rules to the decoded fields of an HTTP/2 request or response.
    ///
    /// HTTP/2 differs from HTTP/1.x in ways that matter to the rules:
    ///   - header names are lower case, so names are compared without regard to case and written in lower case;
    ///   - a request's host is the ":authority" pseudo-header, so a rule for "Host" is applied to it;
    ///   - the connection-specific headers (Connection, Keep-Alive, Transfer-Encoding...) are not allowed, so a rule cannot add them;
    ///   - pseudo-headers (":path", ":status"...) can be updated or deleted by naming them, but only ":authority" can be added.
    /// </summary>
    internal static class Http2HeaderRules
    {
        private static readonly HashSet<string> _connectionSpecific = new(StringComparer.Ordinal)
        {
            "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade"
        };

        public static void Apply(List<HeaderField> fields, IEnumerable<NpHttpHeaderRule> rules, bool isRequest)
        {
            foreach (var rule in rules)
            {
                var name = rule.Name.Trim().ToLowerInvariant();
                if (name.Length == 0)
                {
                    continue;
                }

                var value = Encoding.Latin1.GetBytes(rule.Value ?? string.Empty);

                //A request has no Host header in HTTP/2, the host is the :authority pseudo-header.
                var target = isRequest && name == "host" ? ":authority" : name;
                var isHostRule = target != name;

                if (rule.Action == HttpHeaderAction.Delete)
                {
                    fields.RemoveAll(f => Is(f, target) || (isHostRule && Is(f, name)));
                    continue;
                }

                if (_connectionSpecific.Contains(target))
                {
                    continue; //Not allowed in HTTP/2.
                }

                bool exists = fields.Any(f => Is(f, target) || (isHostRule && Is(f, name)));

                if (exists && rule.Action != HttpHeaderAction.Insert)
                {
                    foreach (var field in fields.Where(f => Is(f, target) || (isHostRule && Is(f, name))))
                    {
                        field.Value = value;
                    }
                }
                else if (!exists && rule.Action != HttpHeaderAction.Update)
                {
                    if (target.StartsWith(':'))
                    {
                        if (target == ":authority")
                        {
                            //Pseudo-headers come before all of the others.
                            int position = fields.FindLastIndex(f => f.Name.Length > 0 && f.Name[0] == ':') + 1;
                            fields.Insert(position, new HeaderField(Encoding.Latin1.GetBytes(target), value));
                        }
                    }
                    else
                    {
                        fields.Add(new HeaderField(Encoding.Latin1.GetBytes(target), value));
                    }
                }
            }
        }

        private static bool Is(HeaderField field, string lowerCaseName)
            => field.NameText.Equals(lowerCaseName, StringComparison.OrdinalIgnoreCase);
    }
}
