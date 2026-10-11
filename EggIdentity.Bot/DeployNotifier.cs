using Discord;
using Discord.WebSocket;
using EggIdentity.Contract;

namespace EggIdentity.Bot;

public sealed class DeployNotifier(
    ChannelConfigStore configStore, DiscordSocketClient client, ulong guildId, string appName) {
    public async Task NotifyAsync(DeployResponse res, CancellationToken ct) {
        var cfg = await configStore.GetAsync(guildId.ToString(), appName, ct);
        if (cfg is null || string.IsNullOrEmpty(cfg.DeployNotificationsThreadId)) return;
        if (!ulong.TryParse(cfg.DeployNotificationsThreadId, out var threadId)) return;

        var guild = client.GetGuild(guildId);
        if (guild?.GetChannel(threadId) is not IMessageChannel channel) return;

        var (messageJson, embedJson, defaultEmbed) = res switch {
            { Ok: true, AlreadyUpToDate: true } => (cfg.UptodateMessageJson, cfg.UptodateEmbedJson, DeployEmbedDefaults.AlreadyUpToDate),
            { Ok: true } => (cfg.SuccessMessageJson, cfg.SuccessEmbedJson, DeployEmbedDefaults.Success),
            _ => (cfg.FailureMessageJson, cfg.FailureEmbedJson, DeployEmbedDefaults.Failure),
        };

        var spec = MessageSpecs.Resolve(messageJson, embedJson, defaultEmbed);
        var rendered = MessageRenderer.Render(spec, DeployVars.Build(res, appName));

        if (rendered.IsComponentsV2 && rendered.Components is not null) {
            await channel.SendMessageAsync(
                components: rendered.Components, flags: MessageFlags.ComponentsV2,
                allowedMentions: rendered.AllowedMentions);
        } else if (rendered.Embed is not null) {
            await channel.SendMessageAsync(embed: rendered.Embed, allowedMentions: rendered.AllowedMentions);
        }
    }
}
