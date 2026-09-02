namespace EggIdentity.Settings.Store;

public sealed class SettingsCache(
    SettingsRegistry registry,
    SettingsStore store,
    Func<string, string?>? file = null,
    TimeSpan? ttl = null,
    TimeProvider? timeProvider = null) : IDisposable {

    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromSeconds(15);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DateTimeOffset _loadedAt;

    public SettingsSnapshot? Current { get; private set; }

    public void Invalidate() => _loadedAt = default;

    public async Task<SettingsSnapshot> GetAsync(CancellationToken ct = default) {
        var cached = Current;
        if (cached is not null && _clock.GetUtcNow() - _loadedAt < _ttl) return cached;

        await _gate.WaitAsync(ct);
        try {
            if (Current is not null && _clock.GetUtcNow() - _loadedAt < _ttl) return Current;
            var database = await store.GetAllAsync(ct);
            Current = new SettingsSnapshot(registry, database, file);
            _loadedAt = _clock.GetUtcNow();
            return Current;
        } finally {
            _gate.Release();
        }
    }

    public async Task<SettingsSnapshot> RefreshAsync(CancellationToken ct = default) {
        Invalidate();
        return await GetAsync(ct);
    }

    public void Dispose() => _gate.Dispose();
}
