using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// What the service is: for health checks and scripts.
    /// </summary>
    public class QueryServiceInfo : IRmQuery<QueryServiceInfoReply>
    {
    }

    public class QueryServiceInfoReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public string Version { get; set; } = string.Empty;
        public long UptimeSeconds { get; set; }
        public int ProxyCount { get; set; }
        public int RunningProxyCount { get; set; }
        public int PluginCount { get; set; }
        public int ManagementPort { get; set; }
        public string ConfigurationName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
    }
}
