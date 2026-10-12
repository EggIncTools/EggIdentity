using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

[JsonConverter(typeof(JsonStringEnumConverter<PinAction>))]
public enum PinAction { NoToken, NoTarget, NoPackages, InSync, Waiting, Commit, Unknown }

public sealed record PinView(
    [property: JsonPropertyName("app")] string App,
    [property: JsonPropertyName("upstream")] string Upstream,
    [property: JsonPropertyName("branch")] string Branch,
    [property: JsonPropertyName("committed")] string? Committed,
    [property: JsonPropertyName("desired")] string? Desired,
    [property: JsonPropertyName("followLatest")] bool FollowLatest,
    [property: JsonPropertyName("latest")] string? Latest,
    [property: JsonPropertyName("pendingVersion")] string? PendingVersion,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("action")] PinAction Action);

public sealed record PinWriteRequest(
    [property: JsonPropertyName("desiredVersion")] string? DesiredVersion,
    [property: JsonPropertyName("followLatest")] bool FollowLatest);

public sealed record TakeLatestRequest([property: JsonPropertyName("app")] string? App);

public sealed record UpstreamBuildView(
    [property: JsonPropertyName("upstream")] string Upstream,
    [property: JsonPropertyName("height")] long Height,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("publishedAt")] DateTimeOffset PublishedAt) {
    [JsonIgnore]
    public string Version => BuildVersion.FromHeight(Height);
}
