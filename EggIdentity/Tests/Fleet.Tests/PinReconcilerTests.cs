using EggIdentity.Contract;
using EggIdentity.Deploy;

namespace EggIdentity.Fleet.Tests;

public sealed class PinReconcilerTests {
    private const string Ledger = "eggledger";
    private const string Identity = "eggidentity";
    private const string Incognito = "eggincognito";
    private const string Branch = "main";
    private const string Props = AppPin.DefaultPropsPath;
    private const string Target = "142.0.0";
    private const string IncognitoTarget = "51.0.0";
    private const string PinCommit = "pincommit";
    private const string Owner = "DavidArthurCole";

    private const string PropsText = """
        <Project>
          <ItemGroup>
            <PackageVersion Include="EggIdentity.Core" Version="140.0.0" />
            <PackageVersion Include="EggIdentity.UI" Version="140.0.0" />
            <PackageVersion Include="EggIncognito.Domain" Version="50.0.0" />
          </ItemGroup>
        </Project>
        """;

    private static readonly RepoRef LedgerRepo = new(Owner, "EggLedger");

    private const string Abacus = "eggabacus";

    private static readonly RepoRef AbacusRepo = new(Owner, "EggAbacus");

    private static readonly IReadOnlyList<SuiteApp> Apps = [
        new SuiteApp { Name = Ledger, RepoUrl = Repo("EggLedger") },
        new SuiteApp { Name = Abacus, RepoUrl = Repo("EggAbacus") },
    ];

    private static readonly IReadOnlyList<SuiteUpstream> Upstreams = [
        new SuiteUpstream { Name = Identity, PackagePrefix = "EggIdentity.", RepoUrl = Repo("EggIdentity") },
        new SuiteUpstream { Name = Incognito, PackagePrefix = "EggIncognito.", RepoUrl = Repo("EggIncognito") },
    ];

    private static string Repo(string name) => new UriBuilder(Uri.UriSchemeHttps, "github.com") { Path = $"{Owner}/{name}" }.Uri.ToString();

    private readonly FakeGitHub _github = new();
    private readonly FakeBuilds _builds = new();
    private readonly FakePinStates _states = new();

    private static AppPin Pin(string upstream = Identity, string? desired = null, bool follow = false) =>
        new() { App = Ledger, Upstream = upstream, Branch = Branch, DesiredVersion = desired, FollowLatest = follow };

    private PinReconciler Reconciler(IReadOnlyList<AppPin> pins, bool token = true) =>
        new(_ => Task.FromResult<(IReadOnlyList<SuiteApp>, IReadOnlyList<SuiteUpstream>, IReadOnlyList<AppPin>)>((Apps, Upstreams, pins)),
            _github, _builds, _states, () => token, new DeployEventRing(firstId: 1));

    [Fact]
    public async Task FollowLatest_CommitsOnceAndRecordsPending() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _builds.Latest[Identity] = 142;
        using var reconciler = Reconciler([Pin(follow: true)]);

        var first = await reconciler.ReconcileAsync(null, CancellationToken.None);
        var second = await reconciler.ReconcileAsync(null, CancellationToken.None);

        var put = Assert.Single(_github.Puts);
        Assert.Equal("pin eggidentity 142.0.0", put.Message);
        Assert.Equal([Target], PinRewrite.Read(put.Text, "EggIdentity."));
        Assert.Equal(["50.0.0"], PinRewrite.Read(put.Text, "EggIncognito."));
        Assert.Equal(PinAction.Commit, Assert.Single(first).Action);
        Assert.Equal(PinAction.Waiting, Assert.Single(second).Action);
        var state = _states.Get(Ledger, Identity);
        Assert.Equal(Target, state?.PendingVersion);
        Assert.Equal(put.Commit, state?.PendingCommit);
    }

    [Fact]
    public async Task ManualPin_AlreadyInSync_MakesNoWrite() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _builds.Latest[Identity] = 142;
        using var reconciler = Reconciler([Pin(desired: "140.0.0")]);

        var views = await reconciler.ReconcileAsync(null, CancellationToken.None);

        Assert.Equal(PinAction.InSync, Assert.Single(views).Action);
        Assert.Empty(_github.Puts);
        Assert.Equal(0, _states.Writes);
    }

    [Fact]
    public async Task RejectedCommit_StoresErrorAndClearsPending() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _github.Reject = "GitHub 409: branch protected";
        _states.Seed(Ledger, Identity, "141.0.0", "oldcommit", null);
        using var reconciler = Reconciler([Pin(desired: Target)]);

        var view = Assert.Single(await reconciler.ReconcileAsync(null, CancellationToken.None));

        var state = _states.Get(Ledger, Identity);
        Assert.Equal("GitHub 409: branch protected", state?.LastError);
        Assert.Null(state?.PendingVersion);
        Assert.Null(state?.PendingCommit);
        Assert.Equal("GitHub 409: branch protected", view.Error);
    }

    [Fact]
    public async Task MissingPropsFile_StoresNotFoundError() {
        using var reconciler = Reconciler([Pin(desired: Target)]);

        var view = Assert.Single(await reconciler.ReconcileAsync(null, CancellationToken.None));

        Assert.Equal("props file Directory.Packages.props not found on main", _states.Get(Ledger, Identity)?.LastError);
        Assert.Equal("props file Directory.Packages.props not found on main", view.Error);
        Assert.Empty(_github.Puts);
    }

    [Fact]
    public async Task NoToken_MakesNoApiCallAndReportsNoToken() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _builds.Latest[Identity] = 142;
        using var reconciler = Reconciler([Pin(follow: true), Pin(Incognito, desired: IncognitoTarget)], token: false);

        var views = await reconciler.ReconcileAsync(null, CancellationToken.None);

        Assert.Equal(2, views.Count);
        Assert.All(views, v => Assert.Equal(PinAction.NoToken, v.Action));
        Assert.Equal(0, _github.Calls);
        Assert.Equal(0, _states.Writes);
    }

    [Fact]
    public async Task NoteHook_DescendantRevision_ClearsPending() {
        _states.Seed(Ledger, Identity, Target, PinCommit, null);
        _github.Ancestors.Add((PinCommit, "laterrev"));
        using var reconciler = Reconciler([Pin(desired: Target)]);

        await reconciler.NoteHookAsync(Ledger, "laterrev", CancellationToken.None);

        var state = _states.Get(Ledger, Identity);
        Assert.Null(state?.PendingVersion);
        Assert.Null(state?.PendingCommit);
    }

    [Fact]
    public async Task NoteHook_ClearingPending_PatchesCachedViews() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        using var reconciler = Reconciler([Pin(desired: Target)]);
        await reconciler.ReconcileAsync(null, CancellationToken.None);
        var commit = Assert.Single(_github.Puts).Commit;
        _github.Ancestors.Add((commit, "laterrev"));

        await reconciler.NoteHookAsync(Ledger, "laterrev", CancellationToken.None);

        var view = Assert.Single(await reconciler.ViewsAsync(CancellationToken.None));
        Assert.Null(view.PendingVersion);
        Assert.Equal(PinAction.Commit, view.Action);
    }

    [Fact]
    public async Task NoteHook_UnrelatedRevision_LeavesPending() {
        _states.Seed(Ledger, Identity, Target, PinCommit, null);
        using var reconciler = Reconciler([Pin(desired: Target)]);

        await reconciler.NoteHookAsync(Ledger, "unrelated", CancellationToken.None);

        var state = _states.Get(Ledger, Identity);
        Assert.Equal(Target, state?.PendingVersion);
        Assert.Equal(PinCommit, state?.PendingCommit);
        Assert.Equal(1, _github.Calls);
    }

    [Fact]
    public async Task TwoUpstreamsOneApp_ReadOnceAndCommitInSequence() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        using var reconciler = Reconciler([Pin(desired: Target), Pin(Incognito, desired: IncognitoTarget)]);

        await reconciler.ReconcileAsync(null, CancellationToken.None);

        Assert.Equal(1, _github.Gets);
        Assert.Equal(2, _github.Puts.Count);
        Assert.Equal("blob0", _github.Puts[0].BlobSha);
        Assert.Equal(_github.Puts[0].NewBlob, _github.Puts[1].BlobSha);
        Assert.Equal([Target], PinRewrite.Read(_github.Puts[1].Text, "EggIdentity."));
        Assert.Equal([IncognitoTarget], PinRewrite.Read(_github.Puts[1].Text, "EggIncognito."));
    }

    [Theory]
    [InlineData("3.9.4")]
    [InlineData("142.0.0+abc1234")]
    public async Task InvalidDesiredVersion_IsRejectedWithoutCommit(string desired) {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        using var reconciler = Reconciler([Pin(desired: desired)]);

        var view = Assert.Single(await reconciler.ReconcileAsync(null, CancellationToken.None));

        Assert.Empty(_github.Puts);
        Assert.Equal($"invalid version \"{desired}\"", _states.Get(Ledger, Identity)?.LastError);
        Assert.Equal($"invalid version \"{desired}\"", view.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadFailure_RecordsErrorForGroupAndReconcilesNextApp(bool timeout) {
        Exception failure = timeout ? new TaskCanceledException("connection reset") : new HttpRequestException("connection reset");
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _github.Seed(AbacusRepo, Props, Branch, PropsText);
        _github.ReadFailures[LedgerRepo] = failure;
        using var reconciler = Reconciler([Pin(desired: Target), Pin(Incognito, desired: IncognitoTarget), Pin(desired: Target) with { App = Abacus }]);

        var views = await reconciler.ReconcileAsync(null, CancellationToken.None);

        var expected = $"{failure.GetType().Name}: connection reset";
        Assert.Equal(expected, _states.Get(Ledger, Identity)?.LastError);
        Assert.Equal(expected, _states.Get(Ledger, Incognito)?.LastError);
        Assert.Equal(3, views.Count);
        var put = Assert.Single(_github.Puts);
        Assert.Equal("pin eggidentity 142.0.0", put.Message);
        Assert.Equal(Target, _states.Get(Abacus, Identity)?.PendingVersion);
    }

    [Fact]
    public async Task CaseVariantStateRows_DoNotThrowAndNewestWins() {
        _states.Seed(Ledger, Identity, null, null, "older", DateTimeOffset.UnixEpoch);
        _states.Seed("EggLedger", "EggIdentity", null, null, "newer", DateTimeOffset.UnixEpoch.AddDays(1));
        using var reconciler = Reconciler([Pin(desired: Target)], token: false);

        var view = Assert.Single(await reconciler.ReconcileAsync(null, CancellationToken.None));

        Assert.Equal("newer", view.Error);
    }

    [Fact]
    public async Task Views_MakeNoGitHubCalls() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        _states.Seed(Ledger, Identity, "141.0.0", PinCommit, null);
        using var reconciler = Reconciler([Pin(desired: Target)]);

        var before = Assert.Single(await reconciler.ViewsAsync(CancellationToken.None));
        Assert.Equal(0, _github.Calls);
        Assert.Null(before.Committed);
        Assert.Equal(PinAction.Unknown, before.Action);

        var reconciled = await reconciler.ReconcileAsync(null, CancellationToken.None);
        var calls = _github.Calls;
        var after = await reconciler.ViewsAsync(CancellationToken.None);

        Assert.Equal(calls, _github.Calls);
        Assert.Equal(reconciled, after);
        Assert.Equal(Target, Assert.Single(after).PendingVersion);
    }

    [Fact]
    public async Task RuntimeTick_SurvivesTimeoutWithoutCancellation() {
        IReadOnlyList<SuiteApp> rows = [];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        using var pins = new PinReconciler(
            _ => throw new TaskCanceledException("github timed out"), _github, _builds, _states, () => true, new DeployEventRing(firstId: 1));
        var runtime = new FleetRuntime(service, () => null, pins: pins);

        await runtime.RefreshOnceAsync(CancellationToken.None);
        await runtime.RefreshOnceAsync(CancellationToken.None);

        Assert.Equal(0, _github.Calls);
    }

    [Fact]
    public async Task ViewsBeforeFirstPass_ReportUnknownWithTokenAndNoTokenWithout() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        using var withToken = Reconciler([Pin(desired: "140.0.0"), Pin(Incognito)]);
        using var withoutToken = Reconciler([Pin(desired: "140.0.0")], token: false);

        var views = await withToken.ViewsAsync(CancellationToken.None);
        var noToken = Assert.Single(await withoutToken.ViewsAsync(CancellationToken.None));

        Assert.Equal([PinAction.Unknown, PinAction.NoTarget], views.Select(v => v.Action));
        Assert.Equal(PinAction.NoToken, noToken.Action);
        Assert.Equal(0, _github.Calls);
    }

    [Fact]
    public async Task RunAsync_TicksPinsWhenDeployServiceIsNotConfigured() {
        _github.Seed(LedgerRepo, Props, Branch, PropsText);
        IReadOnlyList<SuiteApp> rows = [];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        using var pins = Reconciler([Pin(desired: Target)]);
        var runtime = new FleetRuntime(service, () => null, pins: pins);
        using var stop = new CancellationTokenSource();

        var run = runtime.RunAsync(TimeSpan.FromHours(1), stop.Token);
        await stop.CancelAsync();
        await run;

        Assert.False(service.Configured);
        Assert.Equal("pin eggidentity 142.0.0", Assert.Single(_github.Puts).Message);
        Assert.Equal(Target, _states.Get(Ledger, Identity)?.PendingVersion);
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }

    private sealed record PutCall(string Path, string OnBranch, string Text, string BlobSha, string Message, string Commit, string NewBlob);

    private sealed class FakeGitHub : IGitHubRepo {
        private readonly Dictionary<(RepoRef, string, string), RepoFile> _files = [];
        private int _next;

        public List<PutCall> Puts { get; } = [];

        public HashSet<(string Ancestor, string Head)> Ancestors { get; } = [];

        public string? Reject { get; set; }

        public int Gets { get; private set; }

        public int Calls { get; private set; }

        public void Seed(RepoRef repo, string path, string branch, string text) =>
            _files[(repo, path, branch)] = new RepoFile(text, $"blob{_next++}");

        public Dictionary<RepoRef, Exception> ReadFailures { get; } = [];

        public Task<RepoFile?> GetFileAsync(RepoRef repo, string path, string branch, CancellationToken ct) {
            Calls++;
            Gets++;
            if (ReadFailures.TryGetValue(repo, out var failure)) throw failure;
            return Task.FromResult(_files.GetValueOrDefault((repo, path, branch)));
        }

        public Task<RepoCommit> PutFileAsync(
            RepoRef repo, string path, string branch, string text, string blobSha, string message, CancellationToken ct) {
            Calls++;
            if (Reject is not null) throw new GitHubRejectedException(Reject);
            if (_files[(repo, path, branch)].Sha != blobSha) throw new GitHubRejectedException("GitHub 409: sha mismatch");
            var result = new RepoCommit($"commit{_next}", $"blob{_next}");
            _next++;
            _files[(repo, path, branch)] = new RepoFile(text, result.BlobSha);
            Puts.Add(new PutCall(path, branch, text, blobSha, message, result.CommitSha, result.BlobSha));
            return Task.FromResult(result);
        }

        public Task<bool> IsAncestorAsync(RepoRef repo, string ancestor, string head, CancellationToken ct) {
            Calls++;
            return Task.FromResult(Ancestors.Contains((ancestor, head)));
        }
    }

    private sealed class FakeBuilds : IUpstreamBuilds {
        public Dictionary<string, long> Latest { get; } = [with(StringComparer.OrdinalIgnoreCase)];

        public Task RecordAsync(string upstream, long height, string sha, CancellationToken ct) => Task.CompletedTask;

        public Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct) =>
            Task.FromResult(Latest.TryGetValue(upstream, out var h) ? new UpstreamBuild(upstream, h, $"sha{h}", DateTimeOffset.UnixEpoch) : null);

        public Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpstreamBuild>>([]);
    }

    private sealed class FakePinStates : IPinStates {
        private readonly Dictionary<(string, string), PinState> _rows = [];

        public int Writes { get; private set; }

        public void Seed(string app, string upstream, string? version, string? commit, string? error, DateTimeOffset? at = null) =>
            _rows[(app, upstream)] = new PinState(app, upstream, version, commit, error, at ?? DateTimeOffset.UnixEpoch);

        public PinState? Get(string app, string upstream) => _rows.GetValueOrDefault((app, upstream));

        public Task<IReadOnlyList<PinState>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PinState>>([.. _rows.Values]);

        public Task SetPendingAsync(string app, string upstream, string version, string commit, CancellationToken ct) => Write(app, upstream, version, commit, null);

        public Task ClearPendingAsync(string app, string upstream, CancellationToken ct) => Write(app, upstream, null, null, null);

        public Task SetErrorAsync(string app, string upstream, string message, CancellationToken ct) => Write(app, upstream, null, null, message);

        private Task Write(string app, string upstream, string? version, string? commit, string? error) {
            Writes++;
            Seed(app, upstream, version, commit, error);
            return Task.CompletedTask;
        }
    }
}
