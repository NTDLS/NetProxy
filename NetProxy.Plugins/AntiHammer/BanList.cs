using System.Collections.Concurrent;
using System.Text.Json;

namespace NetProxy.Plugins.AntiHammer
{
    /// <summary>
    /// An address that is banned, or was.
    /// </summary>
    public sealed class BanEntry
    {
        /// <summary>The address, or the /64 for an IPv6 one.</summary>
        public string Address { get; set; } = string.Empty;
        public DateTime BannedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }

        /// <summary>How many times the address has been banned, this time included.</summary>
        public int Offences { get; set; }
        public string Reason { get; set; } = string.Empty;

        /// <summary>The connections and requests that were turned away because of the ban. Not kept between runs.</summary>
        internal long Blocked;

        public TimeSpan Duration => ExpiresUtc - BannedUtc;
    }

    /// <summary>
    /// The addresses that are banned, with how often they have been. Bans that have run out are kept for a while, so that an address
    /// that does it again is banned for longer.
    /// </summary>
    internal sealed class BanList(AntiHammerSettings settings, TimeProvider clock)
    {
        private readonly ConcurrentDictionary<string, BanEntry> _entries = new();
        private int _dirty;

        public bool IsDirty => Volatile.Read(ref _dirty) != 0;

        public IReadOnlyCollection<BanEntry> Entries => _entries.Values.ToList();

        public int ActiveCount
        {
            get
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return _entries.Values.Count(o => o.ExpiresUtc > now);
            }
        }

        public BanEntry? GetActive(string key)
            => _entries.TryGetValue(key, out var entry) && entry.ExpiresUtc > clock.GetUtcNow().UtcDateTime ? entry : null;

        public BanEntry Ban(string key, string reason)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var offences = 1;

            if (settings.Escalate && _entries.TryGetValue(key, out var previous) && now - previous.ExpiresUtc <= settings.OffenceMemory)
            {
                offences = previous.Offences + 1;
            }

            var duration = settings.BanDuration;
            for (var i = 1; i < offences && duration < settings.MaxBanDuration; i++)
            {
                duration *= 2;
            }

            if (duration > settings.MaxBanDuration)
            {
                duration = settings.MaxBanDuration;
            }

            var entry = new BanEntry { Address = key, BannedUtc = now, ExpiresUtc = now + duration, Offences = offences, Reason = reason };
            _entries[key] = entry;
            Interlocked.Exchange(ref _dirty, 1);
            return entry;
        }

        /// <summary>
        /// Forgets the bans that ended so long ago that they do not count towards a longer one any more.
        /// </summary>
        public void Purge()
        {
            var now = clock.GetUtcNow().UtcDateTime;

            foreach (var pair in _entries)
            {
                if (now - pair.Value.ExpiresUtc > settings.OffenceMemory)
                {
                    _entries.TryRemove(pair.Key, out _);
                    Interlocked.Exchange(ref _dirty, 1);
                }
            }
        }

        public void Save(string path)
        {
            Interlocked.Exchange(ref _dirty, 0);

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_entries.Values.OrderBy(o => o.BannedUtc).ToList(), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }

        /// <summary>
        /// Reads the bans that were saved. Returns how many are still in force.
        /// </summary>
        public int Load(string path)
        {
            if (File.Exists(path) == false)
            {
                return 0;
            }

            foreach (var entry in JsonSerializer.Deserialize<List<BanEntry>>(File.ReadAllText(path)) ?? [])
            {
                if (string.IsNullOrEmpty(entry.Address) == false)
                {
                    _entries[entry.Address] = entry;
                }
            }

            Purge();
            Interlocked.Exchange(ref _dirty, 0);
            return ActiveCount;
        }
    }
}
