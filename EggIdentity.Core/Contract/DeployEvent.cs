using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

[JsonConverter(typeof(JsonStringEnumConverter<DeployPhase>))]
public enum DeployPhase { ReleaseAvailable, Pulling, Pulled, Recreating, Deployed, Failed, UpToDate, Checked, Restarting }

public sealed record DeployEvent(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("app")] string App,
    [property: JsonPropertyName("phase")] DeployPhase Phase,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("at")] DateTimeOffset At,
    [property: JsonPropertyName("fromRevision")] string? FromRevision,
    [property: JsonPropertyName("toRevision")] string? ToRevision,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("digest")] string? Digest);

public sealed record DeployStatus(
    [property: JsonPropertyName("app")] string App,
    [property: JsonPropertyName("runningDigest")] string? RunningDigest,
    [property: JsonPropertyName("runningRevision")] string? RunningRevision,
    [property: JsonPropertyName("runningVersion")] string? RunningVersion,
    [property: JsonPropertyName("latestDigest")] string? LatestDigest,
    [property: JsonPropertyName("latestRevision")] string? LatestRevision,
    [property: JsonPropertyName("latestVersion")] string? LatestVersion,
    [property: JsonPropertyName("updateAvailable")] bool UpdateAvailable,
    [property: JsonPropertyName("lastCheckedAt")] DateTimeOffset? LastCheckedAt,
    [property: JsonPropertyName("lastEvent")] DeployEvent? LastEvent,
    [property: JsonPropertyName("busy")] bool Busy) {
    [JsonPropertyName("image")] public string? Image { get; init; }
    [JsonPropertyName("stack")] public string? Stack { get; init; }
    [JsonPropertyName("running")] public bool? Running { get; init; }
    [JsonPropertyName("startedAt")] public DateTimeOffset? StartedAt { get; init; }
    [JsonPropertyName("problem")] public string? Problem { get; init; }
}

public sealed record DeployHookPayload(
    [property: JsonPropertyName("app")] string App,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("revision")] string? Revision,
    [property: JsonPropertyName("version")] string? Version);
