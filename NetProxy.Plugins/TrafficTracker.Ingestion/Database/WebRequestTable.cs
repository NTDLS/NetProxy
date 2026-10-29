using NetProxy.Plugins.Common.TrafficTracker;
using NetProxy.Plugins.TrafficTracker.Ingestion.UserAgents;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Plugins.TrafficTracker.Ingestion.Database
{
    /// <summary>
    /// Builds the table that goes to traffic.IngestWebRequests (the traffic.WebRequestTableType): one row for each request, with what the database
    /// needs to normalize it worked out here, where it is cheap to do and easy to test: the hashes that identify a path, a user agent and a
    /// referrer, and what the user agent is.
    /// </summary>
    internal static class WebRequestTable
    {
        public const string TypeName = "traffic.WebRequestTableType";

        public static DataTable Build(string source, IEnumerable<WebRequestRecord> requests)
        {
            var table = new DataTable();

            //The columns are in the order of the table type.
            table.Columns.Add("EventId", typeof(Guid));
            table.Columns.Add("ObservedUtc", typeof(DateTime)).DateTimeMode = DataSetDateTime.Utc;
            table.Columns.Add("Source", typeof(string));
            table.Columns.Add("Proxy", typeof(string));
            table.Columns.Add("ListenPort", typeof(int));
            table.Columns.Add("IsTls", typeof(bool));
            table.Columns.Add("Host", typeof(string));
            table.Columns.Add("Method", typeof(string));
            table.Columns.Add("Path", typeof(string));
            table.Columns.Add("PathHash", typeof(byte[]));
            table.Columns.Add("Query", typeof(string));
            table.Columns.Add("HttpVersion", typeof(byte));
            table.Columns.Add("ClientAddress", typeof(string));
            table.Columns.Add("UserAgent", typeof(string));
            table.Columns.Add("UserAgentHash", typeof(byte[]));
            table.Columns.Add("Browser", typeof(string));
            table.Columns.Add("OperatingSystem", typeof(string));
            table.Columns.Add("DeviceType", typeof(string));
            table.Columns.Add("IsBot", typeof(bool));
            table.Columns.Add("Referrer", typeof(string));
            table.Columns.Add("ReferrerHash", typeof(byte[]));
            table.Columns.Add("ReferrerHost", typeof(string));
            table.Columns.Add("ContentType", typeof(string));
            table.Columns.Add("StatusCode", typeof(short));
            table.Columns.Add("RequestBytes", typeof(long));
            table.Columns.Add("ResponseBytes", typeof(long));
            table.Columns.Add("DurationMs", typeof(int));
            table.Columns.Add("ConnectionId", typeof(Guid));

            var sourceName = Limit(Fallback(source, "(unknown)"), 128);

            foreach (var request in requests)
            {
                var path = Limit(Fallback(request.Path, "/"), 2048);
                var userAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : Limit(request.UserAgent, 1024);
                var referrer = string.IsNullOrWhiteSpace(request.Referrer) ? null : Limit(request.Referrer, 2048);
                var agent = userAgent == null ? null : UserAgentClassifier.Classify(userAgent);

                table.Rows.Add(
                    request.EventId,
                    AsUtc(request.ObservedUtc),
                    sourceName,
                    Limit(Fallback(request.Proxy, "(unknown)"), 128),
                    request.ListenPort,
                    request.IsTls,
                    Limit(Fallback(request.Host, "(unknown)").ToLowerInvariant(), 253),
                    Limit(Fallback(request.Method, "UNKNOWN").ToUpperInvariant(), 32),
                    path,
                    Hash(path),
                    Value(request.Query is { Length: > 0 } query ? Limit(query, 2048) : null),
                    (byte)(request.HttpVersion == "1.0" ? 10 : 11),
                    Limit(Fallback(request.ClientAddress, "(unknown)"), 45),
                    Value(userAgent),
                    Value(userAgent == null ? null : Hash(userAgent)),
                    Value(agent?.Browser),
                    Value(agent?.OperatingSystem),
                    Value(agent?.DeviceType),
                    Value(agent?.IsBot),
                    Value(referrer),
                    Value(referrer == null ? null : Hash(referrer)),
                    Value(referrer == null ? null : HostOf(referrer)),
                    Value(request.ContentType is { Length: > 0 } contentType ? Limit(contentType, 127) : null),
                    Value(request.StatusCode is { } status and >= 0 and <= short.MaxValue ? (short)status : null),
                    Value(request.RequestBytes),
                    Value(request.ResponseBytes),
                    Value(request.DurationMs),
                    request.ConnectionId);
            }

            return table;
        }

        /// <summary>
        /// Identifies a text in an index: a path or a user agent is too long to be one.
        /// </summary>
        internal static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

        private static object Value(object? value) => value ?? DBNull.Value;

        private static string Fallback(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];

        private static DateTime AsUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static string? HostOf(string referrer)
            => Uri.TryCreate(referrer, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.Host) == false
                ? Limit(uri.Host.ToLowerInvariant(), 253)
                : null;
    }
}
