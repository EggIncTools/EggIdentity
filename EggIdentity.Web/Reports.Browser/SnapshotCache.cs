using System.Security.Cryptography;
using System.Text;

namespace EggIdentity.Reports.Browser;

public sealed class SnapshotCache(IEnumerable<ReportSnapshotCapture> captures, BrowserReportOptions options, TimeProvider clock) {
    private const int GateStripes = 64;

    private sealed class Entry(string version, string etag, byte[] bytes) {
        public string Version => version;
        public string ETag => etag;
        public byte[] Bytes => bytes;
        public DateTimeOffset LastUsed { get; set; }
        public long Touch { get; set; }
    }

    private readonly Dictionary<string, ReportSnapshotCapture> _captures =
        captures.ToDictionary(c => c.Source, c => c, StringComparer.Ordinal);

    private readonly Dictionary<string, Entry> _entries = [];
    private readonly SemaphoreSlim[] _gates = [.. Enumerable.Range(0, GateStripes).Select(_ => new SemaphoreSlim(1, 1))];
    private readonly Lock _lock = new();
    private long _clock;

    public async ValueTask<string> VersionAsync(string? partition, string source, IServiceProvider services, CancellationToken ct) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(services);
        var capture = Capture(source);
        return await capture.Version(services, partition, ct).ConfigureAwait(false);
    }

    public async ValueTask<(byte[] Bytes, string ETag)> GetAsync(string scope, string? partition, string source, string version, IServiceProvider services, CancellationToken ct) {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(services);
        var capture = Capture(source);

        var key = Key(scope, partition, source);
        var gate = _gates[(uint)key.GetHashCode(StringComparison.Ordinal) % GateStripes];
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try {
            var now = clock.GetUtcNow();
            if (TryReuse(key, version, now, out var hit)) return hit;

            var bytes = await capture.Capture(services, partition, version, ct).ConfigureAwait(false);
            var entry = new Entry(version, ETagFor(version), bytes) { LastUsed = now };
            lock (_lock) {
                entry.Touch = ++_clock;
                _entries[key] = entry;
                Evict(now);
            }
            return (entry.Bytes, entry.ETag);
        } finally {
            gate.Release();
        }
    }

    public string? CachedVersion(string scope, string? partition, string source) {
        lock (_lock) return _entries.TryGetValue(Key(scope, partition, source), out var entry) ? entry.Version : null;
    }

    public static string ETagFor(string version) {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(version));
        return "\"" + Convert.ToHexStringLower(hash.AsSpan(0, 16)) + "\"";
    }

    private ReportSnapshotCapture Capture(string source) =>
        _captures.TryGetValue(source, out var capture) ? capture : throw new KeyNotFoundException($"unknown report source '{source}'");

    private static string Key(string scope, string? partition, string source) => string.Join('\u001f', scope, partition ?? "", source);

    private bool TryReuse(string key, string version, DateTimeOffset now, out (byte[] Bytes, string ETag) hit) {
        lock (_lock) {
            if (_entries.TryGetValue(key, out var cached) && string.Equals(cached.Version, version, StringComparison.Ordinal)) {
                cached.LastUsed = now;
                cached.Touch = ++_clock;
                Evict(now);
                hit = (cached.Bytes, cached.ETag);
                return true;
            }
        }
        hit = default;
        return false;
    }

    private void Evict(DateTimeOffset now) {
        foreach (var idle in _entries.Where(e => now - e.Value.LastUsed >= options.CacheTtl).Select(e => e.Key).ToList()) _entries.Remove(idle);
        var total = _entries.Values.Sum(e => (long)e.Bytes.Length);
        while (total > options.MaxCacheBytes && _entries.Count > 1) {
            var oldest = _entries.Aggregate((a, b) => a.Value.Touch <= b.Value.Touch ? a : b);
            total -= oldest.Value.Bytes.Length;
            _entries.Remove(oldest.Key);
        }
    }
}
