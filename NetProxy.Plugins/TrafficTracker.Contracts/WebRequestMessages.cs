using NTDLS.CatMQ.Shared;

namespace NetProxy.TrafficTracker.Contracts
{
    /// <summary>
    /// One HTTP request that passed through a NetProxy proxy, with what its response was when that is known.
    ///
    /// The values are the plain text of what was seen. Turning them into rows of a normalized database (one row for each distinct host,
    /// client address, user agent and so on) is the job of the ingestion service, so that the plug-in, which runs on the connection
    /// of every request, does no more than read the headers.
    /// </summary>
    public sealed class WebRequestRecord
    {
        /// <summary>
        /// Identifies this request. The message queue delivers a message at least once, and the ingestion service ignores an event that
        /// it already has.
        /// </summary>
        public Guid EventId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// When the request was received by the proxy.
        /// </summary>
        public DateTime ObservedUtc { get; set; }

        /// <summary>The name of the proxy that handled the request.</summary>
        public string Proxy { get; set; } = string.Empty;

        /// <summary>The port that the proxy listens on.</summary>
        public int ListenPort { get; set; }

        /// <summary>True when the client connection was encrypted (the proxy terminated TLS).</summary>
        public bool IsTls { get; set; }

        /// <summary>The host that the client asked for, in lower case, without a port. The Host header, or the TLS server name when there is none.</summary>
        public string Host { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        /// <summary>The path of the request, without the query string.</summary>
        public string Path { get; set; } = "/";

        /// <summary>The query string, without the question mark. Only recorded when the plug-in is set to.</summary>
        public string? Query { get; set; }

        /// <summary>"1.0" or "1.1".</summary>
        public string HttpVersion { get; set; } = "1.1";

        /// <summary>The address of the client (or of the client that a proxy in front of NetProxy says it is, when that is trusted).</summary>
        public string ClientAddress { get; set; } = string.Empty;

        public string? UserAgent { get; set; }

        /// <summary>The Referer header, without the query string unless the plug-in is set to keep it.</summary>
        public string? Referrer { get; set; }

        /// <summary>The size of the body of the request, when it says (Content-Length).</summary>
        public long? RequestBytes { get; set; }

        /// <summary>The status code of the response. Null when no response was seen (the connection ended first).</summary>
        public int? StatusCode { get; set; }

        /// <summary>The media type of the response ("text/html"), without its parameters.</summary>
        public string? ContentType { get; set; }

        /// <summary>The size of the body of the response, when it says (Content-Length; a chunked body does not).</summary>
        public long? ResponseBytes { get; set; }

        /// <summary>The time from the request being received to the response head coming back, in milliseconds.</summary>
        public int? DurationMs { get; set; }

        /// <summary>The connection that the request was made on. Requests on a keep-alive connection share it.</summary>
        public Guid ConnectionId { get; set; }
    }

    /// <summary>
    /// A batch of requests, which is what the plug-in puts on the queue: one message for many requests keeps the number of
    /// messages (and the work of the queue) small.
    /// </summary>
    public sealed class WebRequestBatch : ICMqMessage
    {
        public Guid BatchId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Which NetProxy installation the requests are from, so that several can share a queue. It is the machine name unless it is set.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        public DateTime SentUtc { get; set; } = DateTime.UtcNow;

        public List<WebRequestRecord> Requests { get; set; } = new();
    }
}
