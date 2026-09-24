namespace EggIdentity.DbClone;

public static class SqlIdentifier {
    public static string Quote(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Contains('\0')
            ? throw new ArgumentException("identifier contains a NUL character", nameof(name))
            : "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string List(IEnumerable<string> names) {
        ArgumentNullException.ThrowIfNull(names);
        return string.Join(", ", names.Select(Quote));
    }
}
