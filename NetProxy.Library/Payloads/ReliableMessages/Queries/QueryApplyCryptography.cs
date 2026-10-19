using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// Tells the service that everything after this message from the console is encrypted (this message itself is not),
    /// and that its reply is to be encrypted.
    /// </summary>
    public class QueryApplyCryptography : IRmQuery<QueryApplyCryptographyReply>
    {
    }

    public class QueryApplyCryptographyReply : IRmQueryReply
    {
    }
}
