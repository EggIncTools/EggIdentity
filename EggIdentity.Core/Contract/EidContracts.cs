using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EggIdentity.Contract;

public static partial class EidRules {
    public const int MaxLabelLength = 64;
    public const int MaxEntries = 32;
    public const string FormatReason = "format";
    public const string LabelReason = "label";
    public const string CapReason = "cap";

    [GeneratedRegex(@"^EI\d{16}$")]
    private static partial Regex Format();

    [GeneratedRegex(@"EI\d{16}")]
    private static partial Regex Embedded();

    public static bool IsValid(string? eid) => eid is not null && Format().IsMatch(eid);

    public static IEnumerable<(string Text, bool IsEid)> Split(string text) {
        ArgumentNullException.ThrowIfNull(text);
        var last = 0;
        foreach (Match match in Embedded().Matches(text)) {
            if (match.Index > last) yield return (text[last..match.Index], false);
            yield return (match.Value, true);
            last = match.Index + match.Length;
        }
        if (last < text.Length) yield return (text[last..], false);
    }

    public static string Mask(string eid) {
        ArgumentNullException.ThrowIfNull(eid);
        return eid.Length <= 8 ? eid : string.Concat(eid.AsSpan(0, 4), new string('*', eid.Length - 8), eid.AsSpan(eid.Length - 4));
    }

    public static string? NormalizeLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();
}

public sealed class EidSnapshot {
    [JsonPropertyName("gameName")]
    public string? GameName { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("ebText")]
    public string? EbText { get; set; }

    [JsonPropertyName("seText")]
    public string? SeText { get; set; }

    [JsonPropertyName("peCount")]
    public int? PeCount { get; set; }

    [JsonPropertyName("teCount")]
    public int? TeCount { get; set; }

    [JsonPropertyName("backupAt")]
    public double? BackupAt { get; set; }
}

public sealed class ProfileEid {
    [JsonPropertyName("eid")]
    public string Eid { get; set; } = "";

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("snapshot")]
    public EidSnapshot? Snapshot { get; set; }

    [JsonPropertyName("snapshotAt")]
    public DateTimeOffset? SnapshotAt { get; set; }

    [JsonIgnore]
    public string? Name => Label ?? Snapshot?.GameName;
}

public sealed class EidUpsert {
    [JsonPropertyName("eid")]
    public string Eid { get; set; } = "";

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("snapshot")]
    public EidSnapshot? Snapshot { get; set; }
}

public sealed class EidRejection {
    [JsonPropertyName("eid")]
    public string Eid { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public sealed class EidUpsertResult {
    [JsonPropertyName("eids")]
    public IReadOnlyList<ProfileEid> Eids { get; set; } = [];

    [JsonPropertyName("rejected")]
    public IReadOnlyList<EidRejection> Rejected { get; set; } = [];
}

public sealed class EidLabelRequest {
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}
