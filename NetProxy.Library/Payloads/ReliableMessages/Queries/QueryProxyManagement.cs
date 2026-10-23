using NetProxy.Library.Payloads.Routing;
using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Adds or replaces a proxy, and says how it went. With <see cref="MatchByName"/> the proxy that has the same name (not case
    /// sensitive) is the one replaced, whatever id the configuration has; otherwise the id decides.
    /// </summary>
    public class QueryUpsertProxy : IRmQuery<QueryUpsertProxyReply>
    {
        public NpProxyConfiguration Proxy { get; set; } = new();
        public bool MatchByName { get; set; }
    }

    public class QueryUpsertProxyReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public bool Success { get; set; }

        /// <summary>"created" or "updated".</summary>
        public string Action { get; set; } = string.Empty;

        public Guid Id { get; set; }

        /// <summary>Why the proxy could not be started, when it was saved but did not start.</summary>
        public string? StartError { get; set; }
    }

    public enum NpProxyAction { Start, Stop, Restart, Delete }

    /// <summary>
    /// Starts, stops, restarts or deletes a proxy, found by name or by id.
    /// </summary>
    public class QueryProxyAction : IRmQuery<QueryActionReply>
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public NpProxyAction Action { get; set; }
    }

    /// <summary>
    /// Checks a proxy configuration against the rules of the service and the other proxies, without saving it.
    /// </summary>
    public class QueryValidateProxy : IRmQuery<QueryValidateProxyReply>
    {
        public NpProxyConfiguration Proxy { get; set; } = new();
        public bool MatchByName { get; set; }
    }

    public class QueryValidateProxyReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<string> Problems { get; set; } = new();
    }

    /// <summary>
    /// Closes one connection, or all the connections of a proxy.
    /// </summary>
    public class QueryCloseConnections : IRmQuery<QueryCloseConnectionsReply>
    {
        public Guid? ConnectionId { get; set; }
        public Guid? ProxyId { get; set; }
        public string? ProxyName { get; set; }
    }

    public class QueryCloseConnectionsReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public int Closed { get; set; }
    }

    /// <summary>
    /// The answer to a request that either worked or did not.
    /// </summary>
    public class QueryActionReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public bool Success { get; set; }
    }
}
