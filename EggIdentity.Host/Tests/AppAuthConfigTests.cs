using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Host.Tests;

public class AppAuthConfigTests {
    private const string Authority = "https://auth.example.com";

    private static string WriteTempDir(params (string fileName, string content)[] files) {
        var dir = Path.Combine(Path.GetTempPath(), "eggidentity-apps-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        foreach (var (fileName, content) in files)
            File.WriteAllText(Path.Combine(dir, fileName), content);
        return dir;
    }

    private static SettingsSnapshot Snapshot(params CollectionRow[] rows) => Snapshot(rows, []);

    private static SettingsSnapshot Snapshot(CollectionRow[] legacy, CollectionRow[] suite) {
        var registry = new SettingsRegistry([], [AuthentikApps.Provider, SuiteApps.Provider]);
        return new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [AuthentikApps.Key] = legacy, [SuiteApps.Key] = suite });
    }

    private static CollectionRow Suite(string name, string? publicUrl, string? clientId = "suite-client", bool enabled = true) =>
        new(SuiteApps.Key, name, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["public_url"] = publicUrl,
            ["enabled"] = enabled ? "true" : "false",
            ["auth_client_id"] = clientId,
            ["auth_client_secret"] = "secret",
            ["auth_callback_url"] = "https://identity.example.com/auth/callback",
        }, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public void FromSnapshot_SuiteRowsWin_KeyedByPublicUrlOrigin() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot([Row("https://legacy.example.com")], [Suite("eggledger", "https://ledger.example.com/app")]), Authority);

        var only = Assert.Single(configs);
        Assert.Equal("https://ledger.example.com", only.Key);
        Assert.Equal("suite-client", only.Value.OAuth.ClientId);
    }

    [Fact]
    public void FromSnapshot_SuiteRowWithoutPublicUrlOrDisabled_IsSkipped() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot([], [Suite("a", null), Suite("b", "https://b.example.com", enabled: false), Suite("c", "https://c.example.com", clientId: null)]),
            Authority);

        Assert.Empty(configs);
    }

    [Fact]
    public void FromSnapshot_NoSuiteLogin_FallsBackToLegacyRows() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot([Row("https://legacy.example.com")], [Suite("eggledger", "https://ledger.example.com", clientId: null)]), Authority);

        Assert.Equal(["https://legacy.example.com"], configs.Keys);
    }

    private static CollectionRow Row(string origin, string? endSession = null) =>
        new(AuthentikApps.Key, origin, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["origin"] = origin,
            ["client_id"] = "client-" + origin.Length,
            ["client_secret"] = "secret",
            ["callback_url"] = "https://identity.example.com/auth/callback",
            ["end_session_url"] = endSession,
        }, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public void FromSnapshot_OneRowPerOrigin_BuildsOAuthAgainstAuthority() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot(Row("https://egg-incognito.example.com"), Row("https://egg-ledger.example.com")), Authority);

        Assert.Equal(2, configs.Count);
        var app = configs["https://egg-ledger.example.com"];
        Assert.Equal("https://egg-ledger.example.com", app.Origin);
        Assert.Equal(Authority, app.OAuth.Authority);
        Assert.Equal("https://identity.example.com/auth/callback", app.OAuth.CallbackUrl);
        Assert.Null(app.EndSessionUrl);
    }

    [Fact]
    public void FromSnapshot_EndSessionUrl_IsCarried() {
        var configs = AppAuthConfigs.FromSnapshot(
            Snapshot(Row("https://egg-incognito.example.com", "https://auth.example.com/application/o/egi/end-session/")), Authority);

        Assert.Equal("https://auth.example.com/application/o/egi/end-session/", configs["https://egg-incognito.example.com"].EndSessionUrl);
    }

    [Fact]
    public void FromSnapshot_EmptyCollection_IsEmpty() =>
        Assert.Empty(AppAuthConfigs.FromSnapshot(Snapshot(), Authority));

    [Fact]
    public void FromRows_SkipsRowsWithoutOrigin() {
        var configs = AppAuthConfigs.FromRows([new AuthentikApp { ClientId = "x" }], Authority);

        Assert.Empty(configs);
    }

    [Fact]
    public void Descriptor_IdIsOriginAndSecretIsClientSecret() {
        Assert.Equal("origin", AuthentikApps.Descriptor.IdField);
        Assert.True(AuthentikApps.Descriptor.FindField("client_secret")!.IsSecret);
        Assert.True(AuthentikApps.Descriptor.HasSecrets);
    }

    [Fact]
    public void LoadFromDirectory_SingleValidFile_ReturnsOneEntryKeyedByOrigin() {
        var dir = WriteTempDir(("egi.app.0", """
            Origin=https://egg-incognito.example.com
            ClientId=egi-client
            ClientSecret=egi-secret
            CallbackUrl=https://identity.example.com/auth/callback
            """));

        var result = AppAuthConfigLoader.LoadFromDirectory(dir, Authority);

        Assert.Single(result);
        Assert.Equal("https://egg-incognito.example.com", result["https://egg-incognito.example.com"].Origin);
    }

    [Fact]
    public void LoadFromDirectory_MissingRequiredKey_Throws() {
        var dir = WriteTempDir(("broken.app.0", """
            Origin=https://egg-incognito.example.com
            ClientId=egi-client
            """));

        Assert.Throws<InvalidOperationException>(() => AppAuthConfigLoader.LoadFromDirectory(dir, Authority));
    }

    [Fact]
    public void LoadFromDirectory_WithEndSessionUrl_PopulatesEndSessionUrl() {
        var dir = WriteTempDir(("egi.app.0", """
            Origin=https://egg-incognito.example.com
            ClientId=egi-client
            ClientSecret=egi-secret
            CallbackUrl=https://identity.example.com/auth/callback
            EndSessionUrl=https://auth.example.com/application/o/egi/end-session/
            """));

        var result = AppAuthConfigLoader.LoadFromDirectory(dir, Authority);

        Assert.Equal("https://auth.example.com/application/o/egi/end-session/", result["https://egg-incognito.example.com"].EndSessionUrl);
    }

    [Fact]
    public void ParseFile_BlankLinesAndWhitespace_AreIgnored() {
        var dir = WriteTempDir(("egi.app.0", """
            Origin = https://egg-incognito.example.com

            ClientId=egi-client

            """));

        var values = AppAuthConfigLoader.ParseFile(Path.Combine(dir, "egi.app.0"));

        Assert.Equal(2, values.Count);
        Assert.Equal("https://egg-incognito.example.com", values["Origin"]);
    }
}
