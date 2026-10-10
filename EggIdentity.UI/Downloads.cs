using System.Text;

namespace EggIdentity.UI;

public sealed class Downloads(DownloadStore store, DownloadOptions options) {
    private const int MaxFrames = 4;
    private const string Binary = "application/octet-stream";
    private readonly List<string> _pending = [];

    public event Action? Changed;

    public IReadOnlyList<string> Pending => _pending;

    public void SaveText(string fileName, string text, string contentType = "text/plain;charset=utf-8") =>
        SaveBytes(fileName, Encoding.UTF8.GetBytes(text), contentType);

    public void SaveBytes(string fileName, ReadOnlyMemory<byte> bytes, string contentType = Binary) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _pending.Add($"{options.Path}/{store.Add(fileName, contentType, bytes)}");
        if (_pending.Count > MaxFrames) _pending.RemoveAt(0);
        Changed?.Invoke();
    }

    public async Task SaveStreamAsync(string fileName, Stream stream, string contentType = Binary, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(stream);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, ct);
        SaveBytes(fileName, copy.GetBuffer().AsMemory(0, (int)copy.Length), contentType);
    }
}
