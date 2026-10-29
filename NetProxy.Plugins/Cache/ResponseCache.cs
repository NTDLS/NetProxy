using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.Cache
{
    /// <summary>
    /// A response that is kept: the head and the body as the endpoint sent them.
    /// </summary>
    internal sealed class CachedResponse
    {
        public required HttpResponseHead Head { get; init; }
        public required byte[] Body { get; init; }
        public required DateTime StoredUtc { get; init; }
        public required DateTime ExpiresUtc { get; init; }

        public long Size => Body.Length + 512; //The head and the bookkeeping, roughly.
    }

    /// <summary>
    /// The responses that are kept, in memory, up to a number of bytes and entries. When it is full the one that was asked for the longest
    /// time ago goes. A response that has expired is never returned.
    /// </summary>
    internal sealed class ResponseCache(long maxBytes, int maxEntries, TimeProvider clock)
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, LinkedListNode<(string Key, CachedResponse Value)>> _entries = [];
        private readonly LinkedList<(string Key, CachedResponse Value)> _recent = new();
        private long _bytes;

        public long Hits { get; private set; }
        public long Misses { get; private set; }
        public long Stored { get; private set; }
        public long Evicted { get; private set; }

        public int Count
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public long Bytes
        {
            get { lock (_lock) { return _bytes; } }
        }

        public CachedResponse? Get(string key)
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(key, out var node) == false)
                {
                    Misses++;
                    return null;
                }

                if (node.Value.Value.ExpiresUtc <= clock.GetUtcNow().UtcDateTime)
                {
                    Remove(node);
                    Misses++;
                    return null;
                }

                _recent.Remove(node);
                _recent.AddFirst(node);
                Hits++;
                return node.Value.Value;
            }
        }

        public void Put(string key, CachedResponse response)
        {
            lock (_lock)
            {
                if (response.Size > maxBytes)
                {
                    return;
                }

                if (_entries.TryGetValue(key, out var existing))
                {
                    Remove(existing);
                }

                var node = _recent.AddFirst((key, response));
                _entries[key] = node;
                _bytes += response.Size;
                Stored++;

                while ((_bytes > maxBytes || _entries.Count > maxEntries) && _recent.Last is { } oldest && oldest != node)
                {
                    Remove(oldest);
                    Evicted++;
                }
            }
        }

        /// <summary>
        /// Drops the responses that have expired.
        /// </summary>
        public int Purge()
        {
            lock (_lock)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var expired = _recent.Where(o => o.Value.ExpiresUtc <= now).ToList();

                foreach (var entry in expired)
                {
                    Remove(_entries[entry.Key]);
                }

                return expired.Count;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _recent.Clear();
                _bytes = 0;
            }
        }

        private void Remove(LinkedListNode<(string Key, CachedResponse Value)> node)
        {
            _recent.Remove(node);
            _entries.Remove(node.Value.Key);
            _bytes -= node.Value.Value.Size;
        }
    }
}
