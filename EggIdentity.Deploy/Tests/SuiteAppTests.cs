using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class SuiteAppTests {
    private static SuiteApp Bind(params (string Field, string? Value)[] pairs) =>
        CollectionBinder.Bind<SuiteApp>(pairs.ToDictionary(p => p.Field, p => p.Value, StringComparer.Ordinal));

    [Fact]
    public void Bind_FillsEveryDeclaredField() {
        var values = SuiteApps.Descriptor.Fields.ToDictionary(f => f.Name, f => (string?)(f.Kind switch {
            SettingKind.Bool => "true",
            SettingKind.Url => $"https://{f.Name.Replace('_', '-')}.example.com/x",
            SettingKind.Enum => f.EnumValues[^1],
            _ => $"v-{f.Name}",
        }), StringComparer.Ordinal);

        var app = CollectionBinder.Bind<SuiteApp>(values);

        Assert.Equal("v-name", app.Name);
        Assert.Equal("v-display_name", app.DisplayName);
        Assert.Equal(SuiteApp.SubProdEnvironment, app.Environment);
        Assert.Equal("https://public-url.example.com/x", app.PublicUrl);
        Assert.Equal("v-stack", app.Stack);
        Assert.Equal("v-container", app.Container);
        Assert.Equal("https://admin-base-url.example.com/x", app.AdminBaseUrl);
        Assert.Equal("v-auth_client_id", app.AuthClientId);
        Assert.Equal("v-auth_client_secret", app.AuthClientSecret);
        Assert.Equal("https://auth-callback-url.example.com/x", app.AuthCallbackUrl);
        Assert.Equal("https://auth-end-session-url.example.com/x", app.AuthEndSessionUrl);
        Assert.Equal(SuiteApp.DiscordBotSuite, app.DiscordBot);
        Assert.True(app.Listed);
        Assert.True(app.ServedBySuiteBot);
        Assert.True(app.IsSubProd);
    }

    [Theory]
    [InlineData("https://ledger.egginc.tools/app?x=1", "https://ledger.egginc.tools")]
    [InlineData("http://localhost:5015", "http://localhost:5015")]
    [InlineData("ledger.egginc.tools", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AuthOrigin_IsSchemeAndAuthorityOfPublicUrl(string? publicUrl, string? origin) =>
        Assert.Equal(origin, new SuiteApp { Name = "a", PublicUrl = publicUrl }.AuthOrigin);

    [Fact]
    public void HasLogin_NeedsClientIdAndOrigin() {
        Assert.False(new SuiteApp { Name = "a", AuthClientId = "c" }.HasLogin);
        Assert.False(new SuiteApp { Name = "a", PublicUrl = "https://a.example.com" }.HasLogin);
        Assert.True(new SuiteApp { Name = "a", PublicUrl = "https://a.example.com", AuthClientId = "c" }.HasLogin);
    }

    [Fact]
    public void ContainerAndLabel_FallBackToName() {
        var app = Bind(("name", "eggledger"));

        Assert.Equal("eggledger", app.ContainerName);
        Assert.Equal("eggledger", app.Label);
        Assert.True(app.Enabled);
        Assert.True(app.AutoDeploy);
        Assert.Equal(SuiteApp.DiscordBotNone, app.DiscordBot);
    }

    [Fact]
    public void Problems_FlagPartialLoginAndMissingOrigin() {
        var partial = SuiteApps.Problems(new SuiteApp { Name = "a", Stack = "s", PublicUrl = "https://a.example.com", AuthClientId = "c" });
        var noOrigin = SuiteApps.Problems(new SuiteApp {
            Name = "a",
            Stack = "s",
            AuthClientId = "c",
            AuthClientSecret = "s",
            AuthCallbackUrl = "https://id.example.com/auth/callback",
        });

        Assert.Contains(partial, p => p.Contains("together", StringComparison.Ordinal));
        Assert.Contains(noOrigin, p => p.Contains("public URL", StringComparison.Ordinal));
    }

    [Fact]
    public void Problems_FlagEnabledAppWithoutStack() {
        Assert.Contains(SuiteApps.Problems(new SuiteApp { Name = "a" }), p => p.Contains("stack", StringComparison.Ordinal));
        Assert.Empty(SuiteApps.Problems(new SuiteApp { Name = "a", Enabled = false }));
    }

    [Fact]
    public void Problems_FlagOwnBotWithoutTokenOrGuild() {
        var bare = new SuiteApp { Name = "a", Stack = "s", DiscordBot = SuiteApp.DiscordBotOwn };
        Assert.Contains(SuiteApps.Problems(bare), p => p.Contains("Discord", StringComparison.Ordinal));
        Assert.DoesNotContain(
            SuiteApps.Problems(bare with { DiscordToken = "t", DiscordGuildId = "1" }),
            p => p.Contains("Discord", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SuiteApp.DiscordBotNone, false)]
    [InlineData(SuiteApp.DiscordBotSuite, false)]
    [InlineData(SuiteApp.DiscordBotOwn, true)]
    public void RegistrationFields_ShowOnlyForOwn(string mode, bool visible) {
        var values = new Dictionary<string, string?> { ["discord_bot"] = mode };
        var registration = SuiteApps.Descriptor.Fields.Where(f => f.Name.StartsWith("discord_", StringComparison.Ordinal) && f.Name != "discord_bot").ToList();

        Assert.Equal(4, registration.Count);
        Assert.All(registration, f => Assert.Equal(visible, f.IsVisible(values)));
        Assert.True(SuiteApps.Descriptor.FindField("discord_bot")?.IsVisible(values));
    }

    [Fact]
    public void Descriptor_EveryFieldHasAGroup() =>
        Assert.All(SuiteApps.Descriptor.Fields, f => Assert.False(string.IsNullOrEmpty(f.Group)));
}
