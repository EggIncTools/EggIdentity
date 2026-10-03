namespace EggIdentity.Testing;

public sealed class TempDir : IDisposable {
    public TempDir(string prefix = "eggid-test") {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string CreateSubdir(params string[] parts) {
        var full = parts.Length == 0 ? File(Guid.NewGuid().ToString("N")) : File(parts);
        Directory.CreateDirectory(full);
        return full;
    }

    public string Write(string relative, string content) {
        var full = File(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full) ?? Path);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public void Dispose() {
        try {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            Leaked = e;
        }
    }

    public Exception? Leaked { get; private set; }

    public override string ToString() => Path;
}
