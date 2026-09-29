using Discord;

namespace EggIdentity.Bot.Tests;

public class CommandRegistrationTests {
    [Fact]
    public void FilterExtras_DropsBuiltinCollisions() {
        var extras = new[] {
            MakeCmd("verify"), // collides, dropped
            MakeCmd("mystats"), // kept
        };
        var kept = EggIdentityBot.FilterExtras(extras).Select(c => c.Name).ToArray();
        Assert.Equal(new[] { "mystats" }, kept);
    }

    [Theory]
    [InlineData(new[] { "a", "b" }, "c", true)]
    [InlineData(new[] { "a", "b" }, "b", false)]
    public void NeedsRole_DetectsAbsence(string[] memberRoles, string roleId, bool expected) =>
        Assert.Equal(expected, EggIdentityBot.NeedsRole(memberRoles, roleId));

    [Fact]
    public void FilterExtras_PreservesAutocompleteHandler() {
        var handler = (SocketAutocompleteContext _) => Task.CompletedTask;
        var cmd = new BotCommand(
            new SlashCommandBuilder().WithName("proto").WithDescription("d").Build(),
            "proto",
            _ => Task.CompletedTask,
            handler);

        var kept = EggIdentityBot.FilterExtras(new[] { cmd }).Single();
        Assert.Same(handler, kept.AutocompleteHandler);
    }

    private static BotCommand MakeCmd(string name) =>
        new(new SlashCommandBuilder().WithName(name).WithDescription("d").Build(), name, _ => Task.CompletedTask);
}
