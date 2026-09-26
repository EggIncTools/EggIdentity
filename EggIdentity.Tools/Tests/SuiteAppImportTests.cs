using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Tools.Tests;

public class SuiteAppImportTests {
    private static CollectionRow Row(string collection, string id, params (string Field, string? Value)[] pairs) =>
        new(collection, id, pairs.ToDictionary(p => p.Field, p => p.Value, StringComparer.Ordinal), DateTimeOffset.UnixEpoch, null);

    private static readonly CollectionRow LedgerDeploy = Row("deploy.apps", "eggledger",
        ("name", "eggledger"), ("image", "ghcr.io/x/eggledger:latest"), ("stack", "egg-apps"),
        ("public_url", "https://ledger.egginc.tools"), ("deploy_secret", "s"), ("auto_deploy", "true"));

    private static readonly CollectionRow LedgerTarget = Row("admin.targets", "eggledger",
        ("name", "eggledger"), ("admin_base_url", "http://eggledger:5015"), ("enabled", "true"));

    private static readonly CollectionRow LedgerAuth = Row("authentik.apps", "https://ledger.egginc.tools",
        ("origin", "https://ledger.egginc.tools"), ("client_id", "c"), ("client_secret", "cs"),
        ("callback_url", "https://id.egginc.tools/auth/callback"));

    [Fact]
    public void Merge_JoinsDeployTargetAndLoginIntoOneRow() {
        var merge = SuiteAppImport.Merge([LedgerDeploy], [LedgerTarget], [LedgerAuth]);

        var (id, values) = Assert.Single(merge.Rows);
        Assert.Equal("eggledger", id);
        Assert.Equal("egg-apps", values["stack"]);
        Assert.Equal("http://eggledger:5015", values["admin_base_url"]);
        Assert.Equal("c", values["auth_client_id"]);
        Assert.Equal("cs", values["auth_client_secret"]);
        Assert.False(values.ContainsKey("image"));
        Assert.False(values.ContainsKey("deploy_secret"));
    }

    [Fact]
    public void Merge_DroppedDeployFields_AreNoted() {
        var merge = SuiteAppImport.Merge([LedgerDeploy], [], []);

        Assert.Contains(merge.Notes, n => n.Contains("image", StringComparison.Ordinal) && n.Contains("deploy_secret", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_OrphanLogin_BecomesItsOwnRowNamedFromHost() {
        var orphan = Row("authentik.apps", "https://incognito.egginc.tools",
            ("origin", "https://incognito.egginc.tools"), ("client_id", "i"), ("client_secret", "s"),
            ("callback_url", "https://id.egginc.tools/auth/callback"));

        var merge = SuiteAppImport.Merge([], [], [orphan]);

        var (id, values) = Assert.Single(merge.Rows);
        Assert.Equal("incognito", id);
        Assert.Equal("https://incognito.egginc.tools", values["public_url"]);
        Assert.Contains(merge.Notes, n => n.Contains("matched no app", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_OrphanAdminTarget_BecomesItsOwnRow() {
        var merge = SuiteAppImport.Merge([], [LedgerTarget], []);

        Assert.Equal(["eggledger"], merge.Rows.Select(r => r.Id));
        Assert.Contains(merge.Notes, n => n.Contains("no deploy row", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_EveryRow_PassesSuiteValidation() {
        var merge = SuiteAppImport.Merge([LedgerDeploy], [LedgerTarget], [LedgerAuth]);

        Assert.All(merge.Rows, r => Assert.Null(SettingsValidation.ValidateRow(SuiteApps.Descriptor, r.Values)));
    }

    [Fact]
    public void Merge_InvalidRow_IsSkippedWithNote() {
        var bad = Row("deploy.apps", "bad", ("name", "bad name"), ("stack", "s"));

        var merge = SuiteAppImport.Merge([bad], [], []);

        Assert.Empty(merge.Rows);
        Assert.Contains(merge.Notes, n => n.StartsWith("bad name: skipped", StringComparison.Ordinal));
    }
}
