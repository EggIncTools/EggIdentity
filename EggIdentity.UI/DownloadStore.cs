using System.Collections.Concurrent;

namespace EggIdentity.UI;

public sealed record PendingDownload(string FileName, string ContentType, ReadOnlyMemory<byte> Bytes, DateTimeOffset Expires);

public sealed class DownloadStore(TimeProvider time) {
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, PendingDownload> _items = new(StringComparer.Ordinal);

    public string Add(string fileName, string contentType, ReadOnlyMemory<byte> bytes) {
        var now = time.GetUtcNow();
        foreach (var (key, item) in _items)
            if (item.Expires <= now) _items.TryRemove(key, out _);
        var id = Guid.NewGuid().ToString("N");
        _items[id] = new PendingDownload(fileName, contentType, bytes, now + Lifetime);
        return id;
    }

    public PendingDownload? Take(string id) =>
        _items.TryRemove(id, out var item) && item.Expires > time.GetUtcNow() ? item : null;
}
