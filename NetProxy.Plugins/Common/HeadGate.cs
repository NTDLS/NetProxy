using NetProxy.PluginApi;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// For the plug-ins that decide about a request before the endpoint sees it. A head that arrives in pieces is held back until it is
    /// whole, so that a plug-in never has to judge half a request, and a request is never half sent on.
    /// </summary>
    internal static class HeadGate
    {
        /// <summary>
        /// Feeds the data to the reader (which has to be made with keepBuffer) and changes the data to what is whole: what was held back
        /// from the last read goes on with this one, and a head that is not whole yet is held back. Returns the requests that are whole.
        /// </summary>
        public static List<SeenRequest> Feed(RequestReader reader, NpDataEventArgs data)
        {
            var requests = reader.Feed(data.Data.Span);

            if (reader.Buffer.Length != data.Data.Length || reader.HeldFrom < reader.Buffer.Length)
            {
                if (reader.HeldFrom == 0)
                {
                    data.Drop();
                }
                else
                {
                    data.Replace(reader.Buffer.AsMemory(0, reader.HeldFrom));
                }
            }

            return requests;
        }
    }
}
