using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Suite.Tests;

public class SuiteEidTests {
    private static readonly string A = "EI0000000000000001";
    private static readonly string B = "EI0000000000000002";
    private static readonly string C = "EI0000000000000003";

    private sealed class FakeStore(bool available = true) : ISuiteEidStore {
        public List<ProfileEid> List { get; } = [];
        public string? Active { get; set; }
        public bool Fail { get; set; }

        public bool IsAvailable => available;

        public Task<ProfileGame?> LoadAsync(CancellationToken ct) =>
            Task.FromResult<ProfileGame?>(new ProfileGame { ActiveEid = Active, Eids = [.. List] });

        public Task<bool> SetActiveAsync(string? eid, string? label, CancellationToken ct) {
            Active = eid;
            if (eid is not null && List.All(e => e.Eid != eid)) List.Add(new ProfileEid { Eid = eid, Label = label });
            return Task.FromResult(true);
        }

        public Task<EidUpsertResult?> UpsertAsync(IReadOnlyList<EidUpsert> entries, CancellationToken ct) {
            if (Fail) return Task.FromResult<EidUpsertResult?>(null);
            var rejected = new List<EidRejection>();
            foreach (var entry in entries) {
                if (!EidRules.IsValid(entry.Eid)) {
                    rejected.Add(new EidRejection { Eid = entry.Eid, Reason = EidRules.FormatReason });
                } else if (List.All(e => e.Eid != entry.Eid)) {
                    List.Add(new ProfileEid { Eid = entry.Eid, Label = entry.Label, Snapshot = entry.Snapshot });
                }
            }
            return Task.FromResult<EidUpsertResult?>(new EidUpsertResult { Eids = [.. List], Rejected = rejected });
        }

        public Task<bool> UpdateSnapshotAsync(string eid, EidSnapshot snapshot, CancellationToken ct) =>
            Task.FromResult(!Fail && List.Any(e => e.Eid == eid));

        public Task<bool> RenameAsync(string eid, string? label, CancellationToken ct) => Task.FromResult(!Fail);

        public Task<bool> RemoveAsync(string eid, CancellationToken ct) => Task.FromResult(!Fail && List.RemoveAll(e => e.Eid == eid) > 0);

        public Task<bool> ReorderAsync(IReadOnlyList<string> eids, CancellationToken ct) => Task.FromResult(!Fail);
    }

    private sealed class FixedUser(CurrentUser user) : ICurrentUser {
        public CurrentUser Current => user;

        public Task<CurrentUser> GetAsync() => Task.FromResult(user);
    }

    private static ServiceProvider Services(FakeStore store) {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(new FixedUser(CurrentUser.Anonymous));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddSingleton<ISuiteEidStore>(store);
        services.AddEggIdentitySuite("ledger");
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static async Task<SuiteContext> LoadedAsync(FakeStore store) {
        var suite = Services(store).CreateScope().ServiceProvider.GetRequiredService<SuiteContext>();
        await suite.LoadAsync();
        return suite;
    }

    [Fact]
    public async Task RegisteredStoreWinsWithNoIdentityClientOrSession() {
        var store = new FakeStore();
        store.List.Add(new ProfileEid { Eid = A, Label = "main" });
        store.Active = A;

        var suite = await LoadedAsync(store);

        Assert.True(suite.CanEditEids);
        Assert.Equal("main", suite.ActiveAccount?.DisplayName);
    }

    [Fact]
    public async Task MutationsUpdateTheListAndRaiseEvents() {
        var store = new FakeStore();
        var suite = await LoadedAsync(store);
        var changed = 0;
        string? removed = null;
        suite.Changed += () => changed++;
        suite.EidRemoved += eid => removed = eid;

        var result = await suite.UpsertEidsAsync([new EidUpsert { Eid = A }, new EidUpsert { Eid = "nope" }, new EidUpsert { Eid = B }]);
        await suite.RemoveEidAsync(A);

        Assert.Equal(["nope"], result!.Rejected.Select(r => r.Eid));
        Assert.Equal([B], suite.Eids.Select(e => e.Eid));
        Assert.Equal(A, removed);
        Assert.Equal(2, changed);
    }

    [Fact]
    public async Task FailedMutationLeavesTheListUnchanged() {
        var store = new FakeStore();
        var suite = await LoadedAsync(store);
        await suite.AddEidAsync(A);
        store.Fail = true;

        Assert.False(await suite.RemoveEidAsync(A));
        Assert.False(await suite.RenameEidAsync(A, "x"));
        Assert.Null(await suite.AddEidAsync(B));
        Assert.Equal([A], suite.Eids.Select(e => e.Eid));
        Assert.Null(suite.Eids[0].Label);
    }

    [Fact]
    public async Task SnapshotUpdateForAMissingEntryDoesNotAddIt() {
        var suite = await LoadedAsync(new FakeStore());
        await suite.AddEidAsync(A);

        Assert.False(await suite.UpdateEidSnapshotAsync(B, new EidSnapshot { GameName = "Ghost" }));
        Assert.True(await suite.UpdateEidSnapshotAsync(A, new EidSnapshot { GameName = "Farmer" }));
        Assert.Equal(["Farmer"], suite.Eids.Select(e => e.DisplayName));
    }

    [Fact]
    public async Task UnavailableStoreRefusesListMutationsButKeepsTheCookieActive() {
        var suite = await LoadedAsync(new FakeStore(available: false));

        Assert.Null(await suite.AddEidAsync(A));
        Assert.False(await suite.RemoveEidAsync(A));
        await suite.SetActiveEidAsync(A);

        Assert.False(suite.CanEditEids);
        Assert.Empty(suite.Eids);
    }

    [Fact]
    public async Task ReorderFollowsTheGivenOrder() {
        var suite = await LoadedAsync(new FakeStore());
        await suite.UpsertEidsAsync([new EidUpsert { Eid = A }, new EidUpsert { Eid = B }, new EidUpsert { Eid = C }]);

        await suite.ReorderEidsAsync([C, A, B]);

        Assert.Equal([C, A, B], suite.Eids.Select(e => e.Eid));
    }

    [Fact]
    public void MaskKeepsOnlyTheEnds() {
        Assert.Equal("EI00**********0001", EidRules.Mask(A));
        Assert.Equal("EI00**********0001", new ProfileEid { Eid = A }.DisplayName);
    }

    private static async Task<string> RenderPickerAsync(FakeStore store, Dictionary<string, object?> parameters) {
        await using var provider = Services(store);
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<SuiteEidPicker>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static FakeStore ThreeAccounts() {
        var store = new FakeStore { Active = B };
        store.List.AddRange([new ProfileEid { Eid = A, Label = "alpha" }, new ProfileEid { Eid = B, Label = "beta" }, new ProfileEid { Eid = C }]);
        return store;
    }

    [Fact]
    public async Task PickerListsInOrderAndChecksTheActiveRow() {
        var html = await RenderPickerAsync(ThreeAccounts(), []);

        var rows = html[html.IndexOf("suite-eid-list", StringComparison.Ordinal)..];
        Assert.True(rows.IndexOf("alpha", StringComparison.Ordinal) < rows.IndexOf("beta", StringComparison.Ordinal));
        Assert.Contains("suite-eid-row suite-eid-row-selected\" role=\"menuitemradio\" aria-checked=\"true\"", html);
        Assert.Single(html.Split("aria-checked=\"true\"").Skip(1));
        Assert.DoesNotContain("suite-eid-filter", html);
    }

    [Fact]
    public async Task PickerTemplatesReplaceTheMaskedText() {
        RenderFragment<ProfileEid> row = e => b => b.AddContent(0, "row:" + e.Eid);
        RenderFragment<ProfileEid?> trigger = e => b => b.AddContent(0, "trigger:" + e?.Eid);

        var html = await RenderPickerAsync(ThreeAccounts(), new() {
            [nameof(SuiteEidPicker.RowTemplate)] = row,
            [nameof(SuiteEidPicker.TriggerTemplate)] = trigger,
        });

        Assert.Contains("trigger:" + B, html);
        Assert.Contains("row:" + C, html);
        Assert.DoesNotContain("suite-eid-masked", html);
    }

    [Fact]
    public async Task PickerWithoutSetsActiveShowsOnlyTheBoundSelection() {
        var html = await RenderPickerAsync(ThreeAccounts(), new() { [nameof(SuiteEidPicker.SetsActive)] = false });

        Assert.Contains("No account", html);
        Assert.DoesNotContain("aria-checked=\"true\"", html);
    }

    [Fact]
    public async Task PickerShowsTheFilterAboveTheThreshold() {
        var html = await RenderPickerAsync(ThreeAccounts(), new() { [nameof(SuiteEidPicker.FilterThreshold)] = 2 });

        Assert.Contains("suite-eid-filter", html);
    }
}
