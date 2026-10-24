using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Adds or replaces a plugin and says how it went. With <see cref="MatchByName"/> the plugin with the same name is the one replaced.
    /// </summary>
    public class QueryUpsertPlugin : IRmQuery<QueryUpsertPluginReply>
    {
        public NpPluginConfiguration Plugin { get; set; } = new();
        public bool MatchByName { get; set; }
    }

    public class QueryUpsertPluginReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public bool Success { get; set; }
        public string Action { get; set; } = string.Empty;

        /// <summary>Why the plugin is not loaded, when it was saved but could not be.</summary>
        public string? LoadError { get; set; }
    }

    public enum NpPluginAction { Delete, Reload, MoveUp, MoveDown }

    public class QueryPluginAction : IRmQuery<QueryActionReply>
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public NpPluginAction Action { get; set; }
    }
}
