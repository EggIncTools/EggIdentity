using Discord;

namespace EggIdentity.Bot;

public static class DefaultEmbeds {
    public static Embed Verify(BotConfig cfg) =>
        new EmbedBuilder()
            .WithTitle(cfg.Name + " Sync Server")
            .WithColor(new Color(0x5865F2))
            .AddField("SHA256", $"[{cfg.Build.Sha256}]({cfg.CommitUrl(cfg.Build.Version)})")
            .AddField("Version", $"[{cfg.Build.Version}]({cfg.CommitUrl(cfg.Build.Version)})", inline: true)
            .AddField("Built", string.IsNullOrEmpty(cfg.Build.Date) ? "unknown" : cfg.Build.Date, inline: true)
            .Build();
}

public sealed record EmbedOptions {
    public uint? Color { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<(string Name, string Value, bool Inline)> ExtraFields { get; init; } = Array.Empty<(string, string, bool)>();

    public Embed Apply(Embed defaultEmbed) {
        var builder = defaultEmbed.ToEmbedBuilder();
        if (Title is not null) builder.WithTitle(Title);
        if (Color is not null) builder.WithColor(new Discord.Color(Color.Value));
        foreach (var (name, value, inline) in ExtraFields)
            builder.AddField(name, value, inline);
        return builder.Build();
    }
}
