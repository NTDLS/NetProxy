using System.Collections.Concurrent;

using NetProxy.Plugins.Common;

namespace NetProxy.Plugins.AntiHammer
{
    /// <summary>
    /// Counts how often things happen in a sliding window: "this address made N requests in the last 10 seconds". The things are
    /// told apart by a key of the form "kind|address|more", so that everything for an address can be dropped at once.
    /// </summary>
    internal sealed class RateTracker(TimeProvider clock)
    {
        /// <summary>The most hits that are remembered for a key. The limits are far below it, and a flood must not use up the memory.</summary>
        private const int MaxHits = 10_000;

        private sealed class Window
        {
            public readonly Queue<long> Hits = new();
            public long Last;
        }

        private readonly ConcurrentDictionary<string, Window> _windows = new();

        public int KeyCount => _windows.Count;

        /// <summary>
        /// Counts one more and says how many there are in the window, this one included.
        /// </summary>
        public int Hit(string key, TimeSpan span)
        {
            var now = clock.GetUtcNow().UtcTicks;
            var window = _windows.GetOrAdd(key, _ => new Window());

            lock (window)
            {
                var oldest = now - span.Ticks;
                while (window.Hits.Count > 0 && window.Hits.Peek() <= oldest)
                {
                    window.Hits.Dequeue();
                }

                window.Hits.Enqueue(now);
                while (window.Hits.Count > MaxHits)
                {
                    window.Hits.Dequeue();
                }

                window.Last = now;
                return window.Hits.Count;
            }
        }

        /// <summary>
        /// Forgets everything that was counted for an address, so that it starts from nothing when its ban ends.
        /// </summary>
        public void Forget(string addressKey)
        {
            foreach (var key in _windows.Keys)
            {
                var parts = key.Split('|', 3);
                if (parts.Length >= 2 && parts[1] == addressKey)
                {
                    _windows.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Drops the counters that have not been touched for a while.
        /// </summary>
        public void Purge(TimeSpan idle)
        {
            var oldest = clock.GetUtcNow().UtcTicks - idle.Ticks;

            foreach (var pair in _windows)
            {
                if (pair.Value.Last < oldest)
                {
                    _windows.TryRemove(pair.Key, out _);
                }
            }
        }
    }
}
