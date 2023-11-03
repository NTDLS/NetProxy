using System;
using System.Text;
using System.Text.RegularExpressions;
using NetProxy.Library;

namespace NetProxy.Service
{
    public static class HttpUtility
    {
        public static string GetNextHeaderToken(string str, ref int position)
        {
            int spacePos = str.IndexOfAny(new char[] { ' ', '\n' }, position);
            if (spacePos >= position)
            {
                string token = str.Substring(position, spacePos - position).Trim();
                position = spacePos + 1;
                return token;
            }

            return null;
        }

        public static bool IsHttpVerb(string str)
        {
            str = str.ToUpper();

            var verbs = Enum.GetValues(typeof(HttpVerb));

            foreach (var verb in verbs)
            {
                if (str == verb.ToString().ToUpper())
                {
                    return true;
                }
            }
            return false;
        }

        public static HttpHeaderType IsHttpHeader(byte[] data, int length, out string verb)
        {
            int maxScanLength = length > 2048 ? 2048 : length;

            string httpHeader = Encoding.UTF8.GetString(data, 0, maxScanLength);

            return IsHttpHeader(httpHeader, out verb);
        }

        public static HttpHeaderType IsHttpHeader(string httpHeader, out string verb)
        {
            int firstLineBreak = httpHeader.IndexOf('\n') + 1;
            if (firstLineBreak > 1)
            {
                httpHeader = httpHeader.Substring(0, firstLineBreak).ToUpper();

                int headerTokPos = 0;
                string token = GetNextHeaderToken(httpHeader, ref headerTokPos);

                if (httpHeader.StartsWith("Http/"))
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

                        if (token.ToUpper().StartsWith("HTTP/"))
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

        /// <summary>
        /// Adds a new Http header value if it does not already exist.
        /// </summary>
        /// <param name="headerText"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="lineBreak"></param>
        /// <returns></returns>
        public static string InsertHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            Regex fieldFidner = new Regex(@"(?i:" + name + @")\:.*");

            Match existingMatch = fieldFidner.Match(headerText);

            string newFieldValue = String.Format("{0}: {1}", name, value);

            if (existingMatch != null)
            {
                ///Do nothing.               
            }
            else
            {
                headerText += newFieldValue + lineBreak;
            }

            return headerText;
        }

        /// <summary>
        /// Adds a new Http header value or updates it if it already exists.
        /// </summary>
        /// <param name="headerText"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="lineBreak"></param>
        /// <returns></returns>
        public static string UpsertHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            Regex fieldFidner = new Regex(@"(?i:" + name + @")\:.*");

            Match existingMatch = fieldFidner.Match(headerText);

            string newFieldValue = String.Format("{0}: {1}", name, value);

            if (existingMatch != null)
            {
                headerText = fieldFidner.Replace(headerText, newFieldValue);
            }
            else
            {
                headerText += newFieldValue + lineBreak;
            }

            return headerText;
        }

        /// <summary>
        /// Adds a new Http header value or updates it if it already exists.
        /// </summary>
        /// <param name="headerText"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="lineBreak"></param>
        /// <returns></returns>
        public static string UpdateHttpHostHeaderValue(string headerText, string name, string value, string lineBreak)
        {
            Regex fieldFidner = new Regex(@"(?i:" + name + @")\:.*");

            Match existingMatch = fieldFidner.Match(headerText);

            string newFieldValue = String.Format("{0}: {1}", name, value);

            if (existingMatch != null)
            {
                headerText = fieldFidner.Replace(headerText, newFieldValue);
            }
            else
            {
                //Do nothing.
            }

            return headerText;
        }

        /// <summary>
        /// Removes a Http header value if it exists, otherwise does nothing.
        /// </summary>
        /// <param name="headerText"></param>
        /// <param name="name"></param>
        /// <param name="lineBreak"></param>
        /// <returns></returns>
        public static string DeleteHttpHostHeaderValue(string headerText, string name, string lineBreak)
        {
            Regex fieldFidner = new Regex(@"(?i:" + name + @")\:.*");

            Match existingMatch = fieldFidner.Match(headerText);

            if (existingMatch != null)
            {
                headerText = fieldFidner.Replace(headerText, string.Empty);
            }

            return headerText;
        }
    }
}
