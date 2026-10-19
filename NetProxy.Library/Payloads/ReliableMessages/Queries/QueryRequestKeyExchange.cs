using NTDLS.ReliableMessaging;

namespace NetProxy.Library.Payloads.ReliableMessages.Queries
{
    /// <summary>
    /// The first thing a console does on a new connection: starts the key exchange that the connection is encrypted with.
    /// </summary>
    public class QueryRequestKeyExchange : IRmQuery<QueryRequestKeyExchangeReply>
    {
        public byte[] NegotiationToken { get; set; } = [];

        public QueryRequestKeyExchange()
        {
        }

        public QueryRequestKeyExchange(byte[] negotiationToken)
        {
            NegotiationToken = negotiationToken;
        }
    }

    public class QueryRequestKeyExchangeReply : IRmQueryReply
    {
        public byte[] NegotiationToken { get; set; } = [];

        public QueryRequestKeyExchangeReply()
        {
        }

        public QueryRequestKeyExchangeReply(byte[] negotiationToken)
        {
            NegotiationToken = negotiationToken;
        }
    }
}
