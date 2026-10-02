namespace EggIdentity.Testing;

public sealed class TempDir : IDisposable {
    public TempDir(string prefix = "eggid-test") {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative) => System.IO.Path.Combine(Path, relative);

    public string Write(string relative, string content) {
        var full = File(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public void Dispose() {
        try {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    public override string ToString() => Path;
}
