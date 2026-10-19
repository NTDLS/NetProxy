using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    public class QueryServiceSettings : IRmQuery<QueryServiceSettingsReply>
    {
    }

    public class QueryServiceSettingsReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public NpServiceSettings Settings { get; set; } = new();
    }
}
