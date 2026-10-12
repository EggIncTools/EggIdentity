using System.Globalization;

namespace EggIdentity.Contract;

public sealed record BuildVersion(long Height, string? Sha) {
    private const int ShortLength = 7;
    private const string Tail = ".0.0";

    public string ShortSha => Sha is null ? "" : Sha[..Math.Min(ShortLength, Sha.Length)];

    public string Display => Sha is null ? $"#{Height}" : $"#{Height} {ShortSha}";

    public override string ToString() => Display;

    public static string FromHeight(long height) => height.ToString(CultureInfo.InvariantCulture) + Tail;

    public static BuildVersion? Parse(string? version) {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var trimmed = version.Trim();
        var plus = trimmed.IndexOf('+', StringComparison.Ordinal);
        var core = plus < 0 ? trimmed : trimmed[..plus];
        if (!core.EndsWith(Tail, StringComparison.Ordinal)) return null;
        if (!long.TryParse(core[..^Tail.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var height)) return null;
        var meta = plus < 0 ? null : trimmed[(plus + 1)..].Split('.')[0];
        return new BuildVersion(height, string.IsNullOrEmpty(meta) ? null : meta);
    }

    public static string Describe(string? version, string? revision) {
        var parsed = Parse(version);
        var sha = parsed?.Sha ?? (string.IsNullOrWhiteSpace(revision) ? null : revision.Trim());
        if (parsed is not null) return (parsed with { Sha = sha }).Display;
        return sha is null ? "" : sha[..Math.Min(ShortLength, sha.Length)];
    }
}
