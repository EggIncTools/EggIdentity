using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Tests;

public class FaultTests {
    private const string Eid = "EI1234567890123456";

    private sealed class Boom : ComponentBase {
        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            throw new InvalidOperationException($"error fetching backup for player {Eid}");
    }

    private sealed class TestNav : NavigationManager {
        public TestNav() => Initialize("https://eggledger.egginc.tools/", "https://eggledger.egginc.tools/missions?eid=" + Eid);

        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class Gate(Func<bool> answer) : IFaultDetailGate {
        public ValueTask<bool> CanSeeDetailAsync() => ValueTask.FromResult(answer());
    }

    private sealed class CapturingLogger : ILogger<CircuitGuard> {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters, Action<ServiceCollection>? configure = null) where T : IComponent {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager, TestNav>();
        services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(new FakeJSRuntime());
        configure?.Invoke(services);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static RenderFragment Faulting => b => {
        b.OpenComponent<Boom>(0);
        b.CloseComponent();
    };

    private static Dictionary<string, object?> Guard(FaultScope scope = FaultScope.Panel, RenderFragment? content = null) => new() {
        [nameof(CircuitGuard.Name)] = "Island",
        [nameof(CircuitGuard.Scope)] = scope,
        [nameof(CircuitGuard.ChildContent)] = content ?? Faulting,
    };

    [Fact]
    public async Task DefaultViewShowsTheLoggedReference() {
        var logger = new CapturingLogger();
        var html = await RenderAsync<CircuitGuard>(Guard(), s => s.AddSingleton<ILogger<CircuitGuard>>(logger));

        var logged = Assert.Single(logger.Messages);
        var id = logged[(logged.IndexOf('[') + 1)..logged.IndexOf(']')];
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", id);
        Assert.Contains(id, html);
        Assert.Contains("fault fault-panel", html);
        Assert.Contains("This part stopped working", html);
    }

    [Fact]
    public async Task RendersWithNoOptionalServicesAndHidesDetail() {
        var html = await RenderAsync<CircuitGuard>(Guard(FaultScope.Page));

        Assert.Contains("fault fault-page", html);
        Assert.Contains("href=\"/\"", html);
        Assert.DoesNotContain("fault-detail", html);
        Assert.DoesNotContain("error fetching backup", html);
    }

    [Fact]
    public async Task GateAllowsDetailWithEidsBlurred() {
        var html = await RenderAsync<CircuitGuard>(Guard(), s => s.AddSingleton<IFaultDetailGate>(new Gate(() => true)));

        Assert.Contains("fault-detail", html);
    }

    [Fact]
    public async Task OpenDetailBlursEids() {
        var html = await RenderAsync<Disclosure>(new() {
            [nameof(Disclosure.Open)] = true,
            [nameof(Disclosure.ChildContent)] = (RenderFragment)(b => {
                b.OpenComponent<EidText>(0);
                b.AddComponentParameter(1, nameof(EidText.Value), Eid);
                b.CloseComponent();
            }),
        });

        Assert.Contains("eid-blur mask", html);
    }

    [Fact]
    public async Task ThrowingGateHidesDetail() {
        var html = await RenderAsync<CircuitGuard>(Guard(), s => s.AddSingleton<IFaultDetailGate>(new Gate(() => throw new InvalidOperationException("no user"))));

        Assert.DoesNotContain("fault-detail", html);
    }

    [Fact]
    public async Task ErrorContentStillOverridesTheDefault() {
        RenderFragment<Exception> custom = e => b => b.AddContent(0, "custom:" + e.GetType().Name);
        var parameters = Guard();
        parameters[nameof(CircuitGuard.ErrorContent)] = custom;

        var html = await RenderAsync<CircuitGuard>(parameters);

        Assert.Contains("custom:InvalidOperationException", html);
        Assert.DoesNotContain("fault-panel", html);
    }

    [Fact]
    public async Task InnerGuardCatchesBeforeOuter() {
        static void Inner(RenderTreeBuilder b) {
            b.OpenComponent<CircuitGuard>(0);
            b.AddComponentParameter(1, nameof(CircuitGuard.Name), "Inner");
            b.AddComponentParameter(2, nameof(CircuitGuard.ChildContent), Faulting);
            b.CloseComponent();
        }

        var html = await RenderAsync<CircuitGuard>(Guard(FaultScope.Page, Inner));

        Assert.Contains("fault fault-panel", html);
        Assert.DoesNotContain("fault fault-page", html);
    }

    [Fact]
    public void CopyPayloadHasNoQueryOrExceptionText() {
        var text = FaultView.BuildCopyText("https://eggledger.egginc.tools/missions?eid=" + Eid + "#x", null, "K7Q2-M9XD", DateTimeOffset.UnixEpoch);

        Assert.Contains("path: /missions\n", text);
        Assert.Contains("reference: K7Q2-M9XD", text);
        Assert.DoesNotContain(Eid, text);
        Assert.DoesNotContain("#x", text);
    }

    [Theory]
    [InlineData(FaultKind.CrashRepeat, "This keeps failing", "Reload")]
    [InlineData(FaultKind.SignInFailed, "Sign-in did not complete", "Try again")]
    [InlineData(FaultKind.NotFound, "Page not found", "Home")]
    public async Task EachKindRendersItsCopyAndAction(FaultKind kind, string title, string action) {
        var html = await RenderAsync<FaultView>(new() {
            [nameof(FaultView.Kind)] = kind,
            [nameof(FaultView.Scope)] = FaultScope.Page,
            [nameof(FaultView.ActionHref)] = "/auth/go/discord",
        });

        Assert.Contains(title, html);
        Assert.Contains(action, html);
    }

    [Fact]
    public async Task ChildContentReplacesTheDefaultActions() {
        RenderFragment buttons = b => b.AddMarkupContent(0, "<a class=\"provider\">Discord</a>");

        var html = await RenderAsync<FaultView>(new() {
            [nameof(FaultView.Kind)] = FaultKind.SignInFailed,
            [nameof(FaultView.ActionHref)] = "/auth/go/discord",
            [nameof(FaultView.ChildContent)] = buttons,
        });

        Assert.Contains("class=\"provider\"", html);
        Assert.DoesNotContain("Try again</a>", html);
    }

    [Fact]
    public async Task NotFoundPageRendersTheSharedView() {
        var html = await RenderAsync<FaultNotFound>([]);

        Assert.Contains("Page not found", html);
        Assert.Contains("fault fault-page", html);
    }

    [Theory]
    [InlineData(SuiteDependency.Database, "The database is not reachable.")]
    [InlineData(SuiteDependency.Identity, "The sign-in service is not responding.")]
    public async Task DependencyNoticeStatesTheReasonAndRetryOnlyWithACallback(SuiteDependency dependency, string reason) {
        var bare = await RenderAsync<DependencyNotice>(new() {
            [nameof(DependencyNotice.Dependency)] = dependency,
            [nameof(DependencyNotice.Feature)] = "Route status",
        });
        var withRetry = await RenderAsync<DependencyNotice>(new() {
            [nameof(DependencyNotice.Dependency)] = dependency,
            [nameof(DependencyNotice.Feature)] = "Route status",
            [nameof(DependencyNotice.OnRetry)] = EventCallback.Factory.Create(this, () => { }),
        });

        Assert.Contains("Route status is unavailable", bare);
        Assert.Contains(reason, bare);
        Assert.DoesNotContain("Retry", bare);
        Assert.Contains("Retry", withRetry);
    }

    [Fact]
    public void FaultIdsAreShortAndDistinct() {
        var ids = Enumerable.Range(0, 200).Select(_ => FaultId.New()).ToList();

        Assert.All(ids, id => Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void SplitSeparatesEmbeddedEids() {
        var parts = EidRules.Split($"a {Eid} b").ToList();

        Assert.Equal([("a ", false), (Eid, true), (" b", false)], parts);
    }
}
