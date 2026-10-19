using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using System.Text;
using System.Text.RegularExpressions;

namespace NetProxy.Service.Http
{
    public static class HttpUtility
    {
        public static readonly List<string> HttpVerbStrings =
        [
            "connect", "delete", "get", "head", "options", "patch", "post", "put", "trace"
        ];

        /// <summary>
        /// The enabled rules that apply to a message of the given type (request or response) and, for requests, method.
        /// </summary>
        public static List<NpHttpHeaderRule> GetApplicableRules(NpProxyConfiguration proxyConfig, HttpHeaderType headerType, string httpRequestVerb)
        {
            return proxyConfig.HttpHeaderRules.Collection.Where(o =>
                (o.HeaderType == headerType || o.HeaderType == HttpHeaderType.Any)
                && (o.Verb.ToString().Equals(httpRequestVerb, StringComparison.CurrentCultureIgnoreCase) || o.Verb == HttpVerb.Any)
                && o.Enabled == true
                ).ToList();
        }

        public static string ApplyHttpHeaderRules(NpProxyConfiguration proxyConfig,
            string httpHeader, HttpHeaderType headerType, string httpRequestVerb, string headerDelimiter)
        {
            try
            {
                var availableRules = GetApplicableRules(proxyConfig, headerType, httpRequestVerb);

                foreach (var rule in availableRules)
                {
                    if (rule.Action == HttpHeaderAction.Upsert)
                    {
                        httpHeader = UpsertHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, headerDelimiter);
                    }
                    else if (rule.Action == HttpHeaderAction.Update)
                    {
                        httpHeader = UpdateHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, headerDelimiter);
                    }
                    else if (rule.Action == HttpHeaderAction.Insert)
                    {
                        httpHeader = InsertHttpHostHeaderValue(httpHeader, rule.Name, rule.Value, headerDelimiter);
                    }
                    else if (rule.Action == HttpHeaderAction.Delete)
                    {
                        httpHeader = DeleteHttpHostHeaderValue(httpHeader, rule.Name, headerDelimiter);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            catch (Exception ex)
            {
                Singletons.Logging.Write("Failed to process HTTP Header rules.", ex);
            }

            return httpHeader;
        }

        public static string? GetNextHeaderToken(string str, ref int position)
        {
            int spacePos = str.IndexOfAny([' ', '\n'], position);
            if (spacePos >= position)
            {
                string token = str.Substring(position, spacePos - position).Trim();
                position = spacePos + 1;
                return token;
            }

            return null;
        }

        public static bool IsHttpVerb(string? str)
        {
            return str != null && HttpVerbStrings.Contains(str, StringComparer.OrdinalIgnoreCase);
        }

        public static HttpHeaderType IsHttpHeader(byte[]? data, int length, out string? verb)
        {
            if (data == null)
            {
                return IsHttpHeader(string.Empty, out verb);
            }

            int maxScanLength = length > 2048 ? 2048 : length;
            string httpHeader = Encoding.UTF8.GetString(data, 0, maxScanLength);
            return IsHttpHeader(httpHeader, out verb);
        }

        public static HttpHeaderType IsHttpHeader(string httpHeader, out string? verb)
        {
            int firstLineBreak = httpHeader.IndexOf('\n') + 1;
            if (firstLineBreak > 1)
            {
                httpHeader = httpHeader.Substring(0, firstLineBreak).ToUpper();

                int headerTokPos = 0;
                var token = GetNextHeaderToken(httpHeader, ref headerTokPos);

                if (httpHeader == null)
                {
                    //Not much to do...
                }
                else if (httpHeader.StartsWith("Http/", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Is response header.
                    verb = string.Empty;
                    return HttpHeaderType.Response;
                }
                else if (IsHttpVerb(token))
                {
                    //Is (potential) response header.

                    verb = token;

                    while (1 == 1)
                    {
                        token = GetNextHeaderToken(httpHeader, ref headerTokPos);
                        if (token == null)
                        {
                            verb = string.Empty;
                            return HttpHeaderType.None;
                        }

                        if (token.StartsWith("HTTP/", StringComparison.InvariantCultureIgnoreCase))
                        {
                            //Is request header
                            return HttpHeaderType.Request;
                        }
                    }
                }

            }

            verb = string.Empty;
            return HttpHeaderType.None;
        }

        /// <summary>
        /// Returns the index of the end of the Http header
        /// </summary>
        /// <param name="header"></param>
        /// <param name="delimiter">Returns the delimiter type (CrLf or Lf)</param>
        /// <returns></returns>
        public static int GetHttpHeaderEnd(string header, out string delimiter)
        {
            int crlf = header.IndexOf("\r\n\r\n");
            int lf = header.IndexOf("\n\n");
            int endOfHeader = 0;

            if (crlf > 0 && (crlf < lf || lf < 0))
            {
                delimiter = "\r\n";
                endOfHeader = crlf + 4;
            }
            else if (lf > 0 && (lf < crlf || crlf < 0))
            {
                delimiter = "\n";
                endOfHeader = lf + 2;
            }
            else
            {
                delimiter = "";
                return -1;
            }

            return endOfHeader;
        }

        private static Regex HeaderLineFinder(string name, bool includeLineBreak)
            => new(@"^" + Regex.Escape(name) + @"[ \t]*:[^\r\n]*" + (includeLineBreak ? @"(\r?\n)?" : string.Empty),
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Appends a header line to the end of the header block (just before the blank line which terminates it).
        /// </summary>
        private static string AppendHttpHeaderLine(string headerText, string name, string value, string lineBreak)
        {
            var newLine = $"{name}: {value}{lineBreak}";

            //The header block ends with a blank line. Insert ahead of it, or at the end if the block was not terminated.
            if (headerText.EndsWith(lineBreak + lineBreak))
            {
                return headerText.Insert(headerText.Length - lineBreak.Length, newLine);
            }

            return headerText + newLine;
        }

        /// <summary>
        /// Adds a new Http header value if it does not already exist.
        /// </summary>
        public static string InsertHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            if (HeaderLineFinder(name, false).IsMatch(headerText))
            {
                return headerText; //Already present, do nothing.
            }

            return AppendHttpHeaderLine(headerText, name, value, lineBreak);
        }

        /// <summary>
        /// Adds a new Http header value or updates it if it already exists.
        /// </summary>
        public static string UpsertHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            var fieldFinder = HeaderLineFinder(name, false);

            if (fieldFinder.IsMatch(headerText))
            {
                return fieldFinder.Replace(headerText, _ => $"{name}: {value}");
            }

            return AppendHttpHeaderLine(headerText, name, value, lineBreak);
        }

        /// <summary>
        /// Updates an Http header value if it exists, otherwise does nothing.
        /// </summary>
        public static string UpdateHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            return HeaderLineFinder(name, false).Replace(headerText, _ => $"{name}: {value}");
        }

        /// <summary>
        /// Removes an Http header (the whole line) if it exists, otherwise does nothing.
        /// </summary>
        public static string DeleteHttpHostHeaderValue(string headerText, string name, string lineBreak)
        {
            return HeaderLineFinder(name, true).Replace(headerText, string.Empty);
        }
    }
}
