using NetProxy.Library;
using NetProxy.Library.Payloads.Routing;
using System.Text;

namespace NetProxy.Service.Proxy
{
    internal static class HttpHeaderAugmentation
    {
        public enum HTTPHeaderResult
        {
            WaitOnData,
            Present,
            NotPresent
        }

        public static HTTPHeaderResult Process(ref StringBuilder httpHeaderBuilder, NpProxyConfiguration proxyConfig, PumpBuffer buffer)
        {
            try
            {
                if (httpHeaderBuilder.Length > 0) //We are reconstructing a fragmented HTTP request header.
                {
                    var stringContent = Encoding.UTF8.GetString(buffer.Bytes, 0, buffer.Length);
                    httpHeaderBuilder.Append(stringContent);
                }
                else if (HttpUtility.StartsWithHTTPVerb(buffer.Bytes))
                {
                    var stringContent = Encoding.UTF8.GetString(buffer.Bytes, 0, buffer.Length);
                    httpHeaderBuilder.Append(stringContent);
                }

                string headerDelimiter = string.Empty;

                if (httpHeaderBuilder.Length > 0)
                {
                    var headerType = HttpUtility.IsHttpHeader(httpHeaderBuilder.ToString(), out string? requestVerb);

                    if (headerType != HttpHeaderType.None && requestVerb != null)
                    {
                        var endOfHeaderIndex = HttpUtility.GetHttpHeaderEnd(httpHeaderBuilder.ToString(), out headerDelimiter);
                        if (endOfHeaderIndex < 0)
                        {
                            return HTTPHeaderResult.WaitOnData; //We have a HTTP header but its a fragment. Wait on the remaining header.
                        }
                        else
                        {
                            if (buffer.Length > headerDelimiter.Length * 2) // "\r\n" or "\r\n\r\n"
                            {
                                //If we received more bytes than just the delimiter then we
                                //  need to determine how many non-header bytes need to be sent to the peer.

                                int endOfHeaderInBufferIndex = HttpUtility.FindDelimiterIndexInByteArray(buffer.Bytes, buffer.Length, $"{headerDelimiter}{headerDelimiter}");
                                if (endOfHeaderInBufferIndex < 0)
                                {
                                    throw new Exception("Could not locate HTTP header in receive buffer.");
                                }

                                int bufferEndOfHeaderOffset = endOfHeaderInBufferIndex + (headerDelimiter.Length * 2);

                                if (bufferEndOfHeaderOffset > buffer.Length)
                                {
                                    //We received extra non-header bytes. We need to remove the header bytes from the buffer
                                    //  and then send them after we modify and send the header.
                                    int newBufferLength = buffer.Length - bufferEndOfHeaderOffset;
                                    Array.Copy(buffer.Bytes, bufferEndOfHeaderOffset, buffer.Bytes, 0, newBufferLength);
                                }

                                buffer.Length -= bufferEndOfHeaderOffset;
                            }
                            else
                            {
                                buffer.Length = 0;
                            }
                        }

                        //Apply the header rules, replacing the original http header builder.
                        httpHeaderBuilder = new StringBuilder(
                            HttpUtility.ApplyHttpHeaderRules(proxyConfig,
                            httpHeaderBuilder.ToString(), headerType, requestVerb, headerDelimiter));

                        return HTTPHeaderResult.Present;
                    }
                }
            }
            catch (Exception ex)
            {
                httpHeaderBuilder.Clear();
                Singletons.Logging.Write("An error occurred while parsing the HTTP request header.", ex);
            }

            return HTTPHeaderResult.NotPresent;
        }
    }
}
