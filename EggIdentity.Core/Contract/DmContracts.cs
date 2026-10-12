using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

public enum DmSendStatus {
    Sent,
    CannotDm,
    UnknownUser,
    RateLimited,
    Failed,
}

public sealed record DmSendResult(DmSendStatus Status) {
    public TimeSpan? RetryAfter { get; init; }
    public int? HttpStatus { get; init; }
    public int? DiscordCode { get; init; }
    public string? ChannelId { get; init; }

    public bool IsSent => Status == DmSendStatus.Sent;
}

public enum DmReachability {
    [JsonStringEnumMemberName("reachable")]
    Reachable,
    [JsonStringEnumMemberName("notInGuild")]
    NotInGuild,
    [JsonStringEnumMemberName("noDiscordLink")]
    NoDiscordLink,
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}

public sealed class DmReachabilityResponse {
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter<DmReachability>))]
    public DmReachability State { get; set; }

    [JsonPropertyName("guildInviteUrl")]
    public string? GuildInviteUrl { get; set; }
}
