using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Tests;

public class DiscordDmSetupCardTests {
    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters) {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<DiscordDmSetupCard>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    [Fact]
    public async Task NotInGuild_ShowsJoinLinkAndRecheck() {
        var html = await RenderAsync(new() {
            [nameof(DiscordDmSetupCard.State)] = (DmReachability?)DmReachability.NotInGuild,
            [nameof(DiscordDmSetupCard.GuildInviteUrl)] = "https://discord.gg/egg",
            [nameof(DiscordDmSetupCard.GuildName)] = "Egg Inc",
        });

        Assert.Contains("href=\"https://discord.gg/egg\"", html);
        Assert.Contains("rel=\"noopener\"", html);
        Assert.Contains("Join server", html);
        Assert.Contains("Re-check", html);
        Assert.DoesNotContain("Send test DM", html);
    }

    [Fact]
    public async Task NoDiscordLink_LinksToProfile() {
        var html = await RenderAsync(new() {
            [nameof(DiscordDmSetupCard.State)] = (DmReachability?)DmReachability.NoDiscordLink,
            [nameof(DiscordDmSetupCard.ProfileUrl)] = "https://hub.example/profile",
        });

        Assert.Contains("href=\"https://hub.example/profile\"", html);
    }

    [Fact]
    public void CannotDm_TellsUserToAllowServerDms() {
        var result = new DmSendResult(DmSendStatus.CannotDm);

        Assert.Equal(StatusNoteKind.Warn, DiscordDmSetupCard.ResultKind(result));
        Assert.Contains("Allow DMs from other members in this server", DiscordDmSetupCard.ResultText(result, "Egg Inc"));
    }
}
