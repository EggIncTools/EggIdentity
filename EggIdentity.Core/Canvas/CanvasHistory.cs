namespace EggIdentity.Canvas;

public sealed class CanvasHistory(CanvasDoc initial, int limit = 100) {
    private readonly List<CanvasDoc> _past = [];
    private readonly List<CanvasDoc> _future = [];

    public CanvasDoc Current { get; private set; } = initial ?? throw new ArgumentNullException(nameof(initial));

    public int Limit { get; } = Math.Max(1, limit);

    public bool CanUndo => _past.Count > 0;

    public bool CanRedo => _future.Count > 0;

    public void Push(CanvasDoc doc) {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.Equals(Current)) return;
        _past.Add(Current);
        if (_past.Count > Limit) _past.RemoveAt(0);
        _future.Clear();
        Current = doc;
    }

    public CanvasDoc Undo() {
        if (!CanUndo) return Current;
        _future.Add(Current);
        Current = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        return Current;
    }

    public CanvasDoc Redo() {
        if (!CanRedo) return Current;
        _past.Add(Current);
        Current = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        return Current;
    }
}
