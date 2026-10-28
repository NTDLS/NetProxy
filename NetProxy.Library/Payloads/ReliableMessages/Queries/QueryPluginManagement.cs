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

    /// <summary>
    /// Sets which proxies use a plugin: exactly the proxies in <see cref="ProxyIds"/> do afterwards. A proxy that is changed is saved
    /// the way an edit of it is, and is running again afterwards if it was running.
    /// </summary>
    public class QuerySetPluginProxies : IRmQuery<QuerySetPluginProxiesReply>
    {
        public Guid PluginId { get; set; }
        public List<Guid> ProxyIds { get; set; } = new();
    }

    public class QuerySetPluginProxiesReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public bool Success { get; set; }

        /// <summary>How many proxies were changed.</summary>
        public int Changed { get; set; }

        /// <summary>The proxies that could not be changed (or started again), each with the reason.</summary>
        public List<string> Problems { get; set; } = new();
    }
}
