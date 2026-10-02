namespace EggIdentity.Resilience;

public sealed class TtlSnapshot<T>(TimeSpan ttl, Func<CancellationToken, Task<T>> load, TimeProvider? time = null, Action<Exception>? onError = null) : IDisposable {
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _hasValue;
    private T _value = default!;

    public bool HasValue => Volatile.Read(ref _hasValue);

    public DateTimeOffset? LoadedAt { get; private set; }

    public async Task<T> GetAsync(CancellationToken ct = default) {
        if (Fresh()) return _value;
        await _gate.WaitAsync(ct);
        try {
            if (Fresh()) return _value;
            try {
                _value = await load(ct);
                Volatile.Write(ref _hasValue, true);
            } catch (Exception e) when (e is not OperationCanceledException && Volatile.Read(ref _hasValue)) {
                onError?.Invoke(e);
            }
            LoadedAt = _time.GetUtcNow();
            return _value;
        } finally {
            _gate.Release();
        }
    }

    public void Invalidate() {
        LoadedAt = null;
    }

    public void Dispose() => _gate.Dispose();

    private bool Fresh() => LoadedAt is { } at && Volatile.Read(ref _hasValue) && _time.GetUtcNow() - at < ttl;
}
