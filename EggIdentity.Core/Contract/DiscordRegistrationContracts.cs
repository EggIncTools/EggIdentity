using System.Text.Json.Serialization;

namespace EggIdentity.Contract;

public sealed class DiscordRegistrationResponse {
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    [JsonPropertyName("guildId")]
    public string GuildId { get; set; } = "";

    [JsonPropertyName("dashboardChannelId")]
    public string? DashboardChannelId { get; set; }
}
