using EggIdentity.Client;
using EggIdentity.Contract;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Consent;

public sealed partial class CookieBanner : IDisposable {
    private const string PrerenderKey = "eggidentity-consent";

    private enum Phase { Unknown, Hidden, Prompt, Configure }

    private sealed record PrerenderDecision(ConsentState? Winner);

    private Phase _phase = Phase.Unknown;
    private bool _functional = true;
    private bool _analytics;
    private bool _userDecided;
    private PersistingComponentStateSubscription _persisting;

    [Inject] private ViewerCookies Cookies { get; set; } = default!;
    [Inject] private ConsentOptions Config { get; set; } = default!;
    [Inject] private ConsentReader Reader { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;
    [Inject] private PersistentComponentState PersistentState { get; set; } = default!;

    [Parameter] public string? SessionToken { get; set; }
    [Parameter] public int PolicyVersion { get; set; } = 1;
    [Parameter] public string? PrivacyUrl { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Body { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Actions { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Options { get; set; }
    [Parameter] public string AcceptAllLabel { get; set; } = "Accept all";
    [Parameter] public string NecessaryOnlyLabel { get; set; } = "Necessary only";
    [Parameter] public string ConfigureLabel { get; set; } = "Configure";
    [Parameter] public string SaveLabel { get; set; } = "Save";
    [Parameter] public string FunctionalLabel { get; set; } = "Functional";
    [Parameter] public string AnalyticsLabel { get; set; } = "Analytics";
    [Parameter] public string PrivacyLabel { get; set; } = "Privacy policy";
    [Parameter] public string? TermsUrl { get; set; }
    [Parameter] public string TermsLabel { get; set; } = "Terms";
    [Parameter] public string Message { get; set; } = "This site uses strictly necessary cookies. Optional functional and analytics cookies are off until you choose.";

    private bool Visible => _phase is Phase.Prompt or Phase.Configure;
    private bool Configuring => _phase == Phase.Configure;
    private int EffectiveVersion => Math.Max(PolicyVersion, Config.PolicyVersion);

    private ConsentBannerContext Context => new(
        _functional, _analytics, Configuring, PrivacyUrl,
        () => DecideAsync(true, true), () => DecideAsync(false, false), Configure, () => DecideAsync(_functional, _analytics),
        v => _functional = v, v => _analytics = v);

    protected override async Task OnInitializedAsync() {
        if (PersistentState.TryTakeFromJson<PrerenderDecision>(PrerenderKey, out var restored) && restored is not null) {
            Decide(restored.Winner);
            return;
        }
        if (RendererInfo.IsInteractive || Services.GetService<IHttpContextAccessor>()?.HttpContext is not { } http) return;

        var winner = ConsentCookie.Parse(http.Request.Cookies[ConsentCookie.Name], EffectiveVersion);
        if (winner is null && SessionToken is { } token && Services.GetService<IdentityApiClient>() is { } api)
            winner = await FetchServerAsync(api, token);

        Decide(winner);
        _persisting = PersistentState.RegisterOnPersisting(() => {
            PersistentState.PersistAsJson(PrerenderKey, new PrerenderDecision(winner));
            return Task.CompletedTask;
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;

        var cookie = ConsentCookie.Parse(Cookies.Get(ConsentCookie.Name), EffectiveVersion);
        var winner = cookie;
        if (SessionToken is { } token && Services.GetService<IdentityApiClient>() is { } api)
            winner = await ReconcileAsync(api, token, cookie);
        if (_userDecided) return;

        Decide(winner);
        StateHasChanged();
    }

    private void Decide(ConsentState? winner) {
        Reader.Set(winner);
        _phase = winner is null ? Phase.Prompt : Phase.Hidden;
    }

    private async Task<ConsentState?> FetchServerAsync(IdentityApiClient api, string token) {
        try {
            return ToState(await api.GetConsentAsync(token, CancellationToken.None));
        } catch (HttpRequestException) {
            return null;
        }
    }

    private async Task<ConsentState?> ReconcileAsync(IdentityApiClient api, string token, ConsentState? cookie) {
        ConsentState? server;
        try {
            server = ToState(await api.GetConsentAsync(token, CancellationToken.None));
        } catch (HttpRequestException) {
            return cookie;
        }

        var result = ConsentReconciler.Reconcile(cookie, server);
        if (result.Winner is null) return null;
        if (result.WriteCookie) WriteCookie(result.Winner);
        if (result.WriteServer) await api.SetConsentAsync(token, ToRequest(result.Winner), CancellationToken.None);
        return result.Winner;
    }

    private void Configure() => _phase = Phase.Configure;

    private async Task DecideAsync(bool functional, bool analytics) {
        _userDecided = true;
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var state = new ConsentState(functional, analytics, EffectiveVersion, now);
        WriteCookie(state);
        if (SessionToken is not null && Services.GetService<IdentityApiClient>() is { } api) await api.SetConsentAsync(SessionToken, ToRequest(state), CancellationToken.None);
        Reader.Set(state);
        _phase = Phase.Hidden;
    }

    private void WriteCookie(ConsentState state) => Cookies.Set(ConsentCookie.Name, ConsentCookie.Format(state));

    private ConsentState? ToState(ConsentResponse? resp) =>
        resp is null || resp.PolicyVersion < EffectiveVersion
            ? null
            : new ConsentState(resp.Functional, resp.Analytics, resp.PolicyVersion, resp.DecidedAt);

    private static ConsentRequest ToRequest(ConsentState state) => new() {
        Functional = state.Functional,
        Analytics = state.Analytics,
        PolicyVersion = state.PolicyVersion,
        DecidedAt = state.DecidedAt,
    };

    public void Dispose() => _persisting.Dispose();
}
