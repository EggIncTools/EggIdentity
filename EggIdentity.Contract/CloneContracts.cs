using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

public sealed record CloneEventWire {
    [JsonPropertyName("at")]
    public DateTimeOffset At { get; init; }

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("table")]
    public string? Table { get; init; }
}

public sealed record CloneTableCountWire {
    [JsonPropertyName("table")]
    public string Table { get; init; } = "";

    [JsonPropertyName("policy")]
    public string Policy { get; init; } = "";

    [JsonPropertyName("sourceRows")]
    public long SourceRows { get; init; }

    [JsonPropertyName("targetRows")]
    public long TargetRows { get; init; }
}

public sealed record CloneStatusResponse {
    [JsonPropertyName("app")]
    public string App { get; init; } = "";

    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; init; }

    [JsonPropertyName("finishedAt")]
    public DateTimeOffset? FinishedAt { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("events")]
    public IReadOnlyList<CloneEventWire> Events { get; init; } = [];

    [JsonPropertyName("tables")]
    public IReadOnlyList<CloneTableCountWire> Tables { get; init; } = [];

    [JsonPropertyName("lastStampAt")]
    public DateTimeOffset? LastStampAt { get; init; }

    [JsonPropertyName("lastStampSource")]
    public string? LastStampSource { get; init; }
}
