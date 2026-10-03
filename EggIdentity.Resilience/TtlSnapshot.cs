namespace EggIdentity.Resilience;

public sealed class TtlSnapshot<T> : IDisposable {
    private readonly TimeSpan _ttl;
    private readonly Func<CancellationToken, Task<T>>? _loadAsync;
    private readonly Func<T>? _load;
    private readonly TimeProvider _time;
    private readonly Action<Exception>? _onError;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _hasValue;
    private T _value = default!;

    public TtlSnapshot(TimeSpan ttl, Func<CancellationToken, Task<T>> load, TimeProvider? time = null, Action<Exception>? onError = null) {
        _ttl = ttl;
        _loadAsync = load;
        _time = time ?? TimeProvider.System;
        _onError = onError;
    }

    public TtlSnapshot(TimeSpan ttl, Func<T> load, TimeProvider? time = null, Action<Exception>? onError = null) {
        _ttl = ttl;
        _load = load;
        _time = time ?? TimeProvider.System;
        _onError = onError;
    }

    public bool HasValue => Volatile.Read(ref _hasValue);

    public DateTimeOffset? LoadedAt { get; private set; }

    public T Get() {
        if (_load is null) throw new InvalidOperationException("TtlSnapshot was built with an async loader; call GetAsync.");
        if (Fresh()) return _value;
        _gate.Wait();
        try {
            if (Fresh()) return _value;
            try {
                Store(_load());
            } catch (Exception e) when (Volatile.Read(ref _hasValue)) {
                _onError?.Invoke(e);
            }
            LoadedAt = _time.GetUtcNow();
            return _value;
        } finally {
            _gate.Release();
        }
    }

    public async Task<T> GetAsync(CancellationToken ct = default) {
        if (Fresh()) return _value;
        await _gate.WaitAsync(ct);
        try {
            if (Fresh()) return _value;
            try {
                Store(_loadAsync is not null ? await _loadAsync(ct) : (_load ?? throw new InvalidOperationException("TtlSnapshot has no loader."))());
            } catch (Exception e) when (e is not OperationCanceledException && Volatile.Read(ref _hasValue)) {
                _onError?.Invoke(e);
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

    private void Store(T value) {
        _value = value;
        Volatile.Write(ref _hasValue, true);
    }

    private bool Fresh() => LoadedAt is { } at && Volatile.Read(ref _hasValue) && _time.GetUtcNow() - at < _ttl;
}
