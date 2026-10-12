using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class SuiteUpstreamsTests {
    private const string Ledger = "eggledger";
    private const string Identity = "eggidentity";

    private static readonly IReadOnlyList<SuiteApp> Apps = [
        new SuiteApp { Name = Ledger, RepoUrl = "https://github.com/DavidArthurCole/EggLedger" },
        new SuiteApp { Name = Identity, RepoUrl = "https://github.com/DavidArthurCole/EggIdentity" },
        new SuiteApp { Name = "norepo" },
    ];

    private static readonly IReadOnlyList<SuiteUpstream> Upstreams = [
        new SuiteUpstream { Name = Identity, PackagePrefix = "EggIdentity.", RepoUrl = "https://github.com/DavidArthurCole/EggIdentity" },
    ];

    private static AppPin Pin(string app, string upstream = Identity, bool follow = false) =>
        new() { App = app, Upstream = upstream, Branch = "main", FollowLatest = follow };

    [Fact]
    public void Problems_ValidPin_IsEmpty() =>
        Assert.Empty(SuiteUpstreams.Problems(Pin(Ledger, follow: true), Apps, Upstreams));

    [Fact]
    public void Problems_UnknownApp_IsReported() {
        var problems = SuiteUpstreams.Problems(Pin("ghost"), Apps, Upstreams);

        Assert.Equal(["No suite app named \"ghost\"."], problems);
    }

    [Fact]
    public void Problems_UnknownUpstream_IsReported() {
        var problems = SuiteUpstreams.Problems(Pin(Ledger, "nope"), Apps, Upstreams);

        Assert.Equal(["No upstream named \"nope\"."], problems);
    }

    [Fact]
    public void Problems_AppWithoutRepoUrl_IsReported() {
        var problems = SuiteUpstreams.Problems(Pin("norepo"), Apps, Upstreams);

        Assert.Single(problems);
        Assert.Contains("repository URL", problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_SelfPinWithoutFollowLatest_IsClean() =>
        Assert.Empty(SuiteUpstreams.Problems(Pin(Identity), Apps, Upstreams));

    [Fact]
    public void Bind_PinRow_AppliesDefaultsAndComputesId() {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["id"] = "eggledger/eggidentity",
            ["app"] = Ledger,
            ["upstream"] = Identity,
            ["branch"] = "main",
            ["desired_version"] = "142.0.0",
        };

        var pin = CollectionBinder.Bind<AppPin>(values);

        Assert.Equal(AppPin.DefaultPropsPath, pin.PropsPath);
        Assert.False(pin.FollowLatest);
        Assert.Equal("142.0.0", pin.DesiredVersion);
        Assert.Equal("eggledger/eggidentity", pin.Id);
    }

    [Fact]
    public void PinsDescriptor_ComputedIdPassesRowValidation() {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["id"] = SuiteUpstreams.PinId(Ledger, Identity),
            ["app"] = Ledger,
            ["upstream"] = Identity,
            ["branch"] = "main",
        };

        Assert.Null(SettingsValidation.ValidateRow(SuiteUpstreams.PinsDescriptor, values));
    }
}
