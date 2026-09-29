using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Host.Tests;

public class AppAuthConfigTests {
    private const string Authority = "https://auth.example.com";
    private const string Callback = "https://identity.example.com/auth/callback";

    private static SettingsSnapshot Snapshot(params CollectionRow[] suite) {
        var registry = new SettingsRegistry([], [SuiteApps.Provider]);
        return new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [SuiteApps.Key] = suite });
    }

    private static CollectionRow Suite(
        string name, string? publicUrl, string? clientId = "suite-client", bool enabled = true, string? endSession = null) =>
        new(SuiteApps.Key, name, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["public_url"] = publicUrl,
            ["enabled"] = enabled ? "true" : "false",
            ["auth_client_id"] = clientId,
            ["auth_client_secret"] = "secret",
            ["auth_callback_url"] = Callback,
            ["auth_end_session_url"] = endSession,
        }, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public void FromSnapshot_KeysByPublicUrlOrigin_AndBuildsOAuthAgainstAuthority() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot(Suite("eggledger", "https://ledger.example.com/app"), Suite("eggincognito", "https://egi.example.com")), Authority);

        Assert.Equal(2, configs.Count);
        var ledger = configs["https://ledger.example.com"];
        Assert.Equal("https://ledger.example.com", ledger.Origin);
        Assert.Equal("suite-client", ledger.OAuth.ClientId);
        Assert.Equal(Authority, ledger.OAuth.Authority);
        Assert.Equal(Callback, ledger.OAuth.CallbackUrl);
        Assert.Null(ledger.EndSessionUrl);
    }

    [Fact]
    public void FromSnapshot_RowWithoutPublicUrlOrClientIdOrDisabled_IsSkipped() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot(Suite("a", null), Suite("b", "https://b.example.com", enabled: false), Suite("c", "https://c.example.com", clientId: null)),
            Authority);

        Assert.Empty(configs);
    }

    [Fact]
    public void FromSnapshot_EndSessionUrl_IsCarried() {
        var end = "https://auth.example.com/application/o/egi/end-session/";
        var configs = AppAuthConfigs.FromSnapshot(Snapshot(Suite("egi", "https://egi.example.com", endSession: end)), Authority);

        Assert.Equal(end, configs["https://egi.example.com"].EndSessionUrl);
    }

    [Fact]
    public void FromSnapshot_TwoRowsOnOneOrigin_LastWins() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot(Suite("first", "https://same.example.com", clientId: "one"), Suite("second", "https://same.example.com", clientId: "two")),
            Authority);

        var only = Assert.Single(configs);
        Assert.Equal("two", only.Value.OAuth.ClientId);
    }
}
