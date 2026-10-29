using System.Text;

namespace NetProxy.Plugins.Common
{
    /// <summary>
    /// The head of an HTTP/1.x response (the status line and the headers), in the order the headers came in, with the same header more than
    /// once if it was (Set-Cookie), so that it can be changed and written out again as it was.
    /// </summary>
    internal sealed class HttpResponseHead
    {
        public string Version { get; set; } = "HTTP/1.1";
        public int Status { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<KeyValuePair<string, string>> Headers { get; } = [];

        public string? Get(string name)
        {
            foreach (var header in Headers)
            {
                if (header.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        public bool Has(string name) => Get(name) != null;

        public List<string> GetAll(string name)
            => Headers.Where(o => o.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(o => o.Value).ToList();

        public void Remove(string name) => Headers.RemoveAll(o => o.Key.Equals(name, StringComparison.OrdinalIgnoreCase));

        public void Add(string name, string value) => Headers.Add(new KeyValuePair<string, string>(name, value));

        /// <summary>Replaces the header (all of it, if it was sent more than once) or adds it.</summary>
        public void Set(string name, string value)
        {
            Remove(name);
            Add(name, value);
        }

        public long? ContentLength => Get("Content-Length") is { } value && long.TryParse(value, out var length) && length >= 0 ? length : null;

        /// <param name="head">The head without the empty line that ends it.</param>
        public static HttpResponseHead? TryParse(ReadOnlySpan<byte> head)
        {
            var lines = Encoding.Latin1.GetString(head).Split("\r\n");
            var parts = lines[0].Split(' ', 3);

            if (parts.Length < 2 || parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) == false || int.TryParse(parts[1], out var status) == false)
            {
                return null;
            }

            var result = new HttpResponseHead { Version = parts[0], Status = status, Reason = parts.Length > 2 ? parts[2] : string.Empty };

            for (var i = 1; i < lines.Length; i++)
            {
                var colon = lines[i].IndexOf(':');

                //A header that is folded onto the next line (obsolete) is not understood, and neither is a line that is not a header.
                if (colon <= 0 || lines[i][0] is ' ' or '\t')
                {
                    return null;
                }

                result.Add(lines[i][..colon].Trim(), lines[i][(colon + 1)..].Trim());
            }

            return result;
        }

        /// <summary>The head as it goes on the wire, with the empty line that ends it.</summary>
        public byte[] ToBytes()
        {
            var text = new StringBuilder();
            text.Append(Version).Append(' ').Append(Status).Append(' ').Append(Reason).Append("\r\n");

            foreach (var header in Headers)
            {
                text.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }

            return Encoding.Latin1.GetBytes(text.Append("\r\n").ToString());
        }

        /// <summary>
        /// Finds the end of a head in what has been read so far: the index of the empty line, or -1.
        /// </summary>
        public static int FindEnd(ReadOnlySpan<byte> data) => data.IndexOf("\r\n\r\n"u8);
    }
}
