using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Host;

public sealed record SponsorConfig(
    string GitHubPat,
    string GitHubTarget,
    string GitHubWebhookSecret,
    string DiscordBotToken,
    string DiscordGuildId,
    string DiscordRoleId) {
    public const string DefaultTarget = "DavidArthurCole";

    public static SponsorConfig? From(ISettingsSource settings, DiscordRegistrationResponse? registration) {
        ArgumentNullException.ThrowIfNull(settings);
        var pat = settings.Value(HostSettings.SponsorPat).Value;
        var webhookSecret = settings.Value(HostSettings.SponsorWebhookSecret).Value;
        var roleId = settings.Value(HostSettings.SponsorRoleId).Value;
        if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(webhookSecret) || string.IsNullOrEmpty(roleId)
            || registration is not { Token.Length: > 0, GuildId.Length: > 0 }) {
            return null;
        }

        var target = settings.Value(HostSettings.SponsorTarget).Value;
        return new SponsorConfig(
            GitHubPat: pat,
            GitHubTarget: string.IsNullOrEmpty(target) ? DefaultTarget : target,
            GitHubWebhookSecret: webhookSecret,
            DiscordBotToken: registration.Token,
            DiscordGuildId: registration.GuildId,
            DiscordRoleId: roleId);
    }
}
