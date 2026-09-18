using EggIdentity.Client;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace EggIdentity.Consent;

public sealed partial class CookieBanner {
    private enum Phase { Unknown, Hidden, Prompt, Configure }

    private Phase _phase = Phase.Unknown;
    private bool _functional = true;
    private bool _analytics;

    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ConsentOptions Config { get; set; } = default!;
    [Inject] private ConsentReader Reader { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter] public string? SessionToken { get; set; }
    [Parameter] public int PolicyVersion { get; set; } = 1;
    [Parameter] public string? PrivacyUrl { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Body { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Actions { get; set; }
    [Parameter] public RenderFragment<ConsentBannerContext>? Options { get; set; }

    private bool Visible => _phase is Phase.Prompt or Phase.Configure;
    private bool Configuring => _phase == Phase.Configure;
    private int EffectiveVersion => Math.Max(PolicyVersion, Config.PolicyVersion);

    private ConsentBannerContext Context => new(
        _functional, _analytics, Configuring, PrivacyUrl,
        AcceptAllAsync, NecessaryOnlyAsync, Configure, SaveAsync,
        v => _functional = v, v => _analytics = v);

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender) return;

        ConsentState? cookie;
        try {
            cookie = ConsentCookie.Parse(await Js.InvokeAsync<string?>("eggConsentRead"), EffectiveVersion);
        } catch (JSException) {
            return;
        }

        var winner = cookie;
        var api = SessionToken is null ? null : Services.GetService<IdentityApiClient>();
        if (api is not null) winner = await ReconcileAsync(api, SessionToken!, cookie);

        Reader.Set(winner);
        _phase = winner is null ? Phase.Prompt : Phase.Hidden;
        StateHasChanged();
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
        if (result.WriteCookie) await WriteCookieAsync(result.Winner);
        if (result.WriteServer) await api.SetConsentAsync(token, ToRequest(result.Winner), CancellationToken.None);
        return result.Winner;
    }

    private Task AcceptAllAsync() => DecideAsync(true, true);

    private Task NecessaryOnlyAsync() => DecideAsync(false, false);

    private Task SaveAsync() => DecideAsync(_functional, _analytics);

    private void Configure() => _phase = Phase.Configure;

    private async Task DecideAsync(bool functional, bool analytics) {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var state = new ConsentState(functional, analytics, EffectiveVersion, now);
        await WriteCookieAsync(state);
        if (SessionToken is not null && Services.GetService<IdentityApiClient>() is { } api) {
            await api.SetConsentAsync(SessionToken, ToRequest(state), CancellationToken.None);
        }
        Reader.Set(state);
        _phase = Phase.Hidden;
    }

    private async Task WriteCookieAsync(ConsentState state) {
        try {
            await Js.InvokeVoidAsync("eggConsentWrite", ConsentCookie.Format(state), Config.CookieDomain, (int)Config.CookieLifetime.TotalDays);
        } catch (JSException) {
        }
    }

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
}
