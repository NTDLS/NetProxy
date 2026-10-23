using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// The end of the log file of the service.
    /// </summary>
    public class QueryLogTail : IRmQuery<QueryLogTailReply>
    {
        public int Lines { get; set; } = 100;
    }

    public class QueryLogTailReply : IRmQueryReply
    {
        public string? Message { get; set; }
        public List<string> Lines { get; set; } = new();
    }
}
