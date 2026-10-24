using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// The plug-ins of the service, in the order that they are called in.
    /// </summary>
    public class QueryPluginList : IRmQuery<QueryPluginListReply>
    {
    }

    public class QueryPluginListReply : IRmQueryReply
    {
        public string? Message { get; set; }

        public List<NpPluginGridItem> Collection { get; set; } = new();
    }
}
