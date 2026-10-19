using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.UI.Suite;

public sealed class SuiteOptions {
    public string CurrentSlug { get; set; } = "";
    public string DefaultTheme { get; set; } = "dark";
    public IReadOnlyList<string> Themes { get; set; } = ["dark", "light"];
}

public sealed class SuiteContext(
    SuiteOptions options,
    ICurrentUser user,
    ViewerCookies cookies,
    IdentityApiClient? api = null,
    IHttpContextAccessor? accessor = null,
    SessionCookieOptions? session = null) {
    private readonly string? _token = ReadToken(accessor, session);
    private bool _loaded;
    private bool? _motion;
    private string? _theme;
    private ProfileGame? _game;

    public event Action? Changed;

    public IReadOnlyList<SuiteEntry> Tools { get; private set; } = SuiteCatalog.Fallback;

    public string CurrentSlug => options.CurrentSlug;

    public SuiteEntry? Current => Tools.FirstOrDefault(t => string.Equals(t.Slug, options.CurrentSlug, StringComparison.OrdinalIgnoreCase));

    public CurrentUser Viewer => user.Current;

    public IReadOnlyList<string> Themes => options.Themes;

    public bool CanEditProfile => SessionToken() is not null && api is not null;

    public string Theme => _theme ?? cookies.Get(ViewerCookieOptions.ThemeCookie) ?? options.DefaultTheme;

    public bool ReducedMotion => _motion ?? cookies.Get(ViewerCookieOptions.MotionCookie) == "1";

    public string? ActiveEid => _game?.ActiveEid ?? cookies.Get(ViewerCookieOptions.EidCookie);

    public string? ActiveEidLabel => _game?.ActiveEidLabel;

    public bool IsSpeculative => Speculation.IsSpeculative(accessor?.HttpContext);

    public IReadOnlyDictionary<string, object> HtmlAttributes => new Dictionary<string, object>(StringComparer.Ordinal) {
        [ThemeAttribute] = Theme,
        [MotionAttribute] = ReducedMotion ? "reduce" : "auto",
    };

    private const string DefaultSessionCookie = "eggidentity_session";

    public const string ThemeAttribute = "data-suite-theme";
    public const string MotionAttribute = "data-suite-motion";

    public async Task LoadAsync(CancellationToken ct = default) {
        if (_loaded) return;
        _loaded = true;
        Tools = await SuiteCatalog.LoadAsync(api, ct);
        if (SessionToken() is not { } token || api is null) return;
        try {
            var profile = await api.GetProfileAsync(token, ct);
            _theme = profile?.Theme ?? _theme;
            _motion = profile?.ReducedMotion ?? _motion;
            _game = await api.GetGameAsync(token, ct);
        } catch (Exception e) when (e is HttpRequestException or TaskCanceledException) {
            _game = null;
        }
    }

    public async Task SetThemeAsync(string theme, CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(theme);
        _theme = theme;
        cookies.Set(ViewerCookieOptions.ThemeCookie, theme);
        if (SessionToken() is { } token && api is not null) await api.SetPreferencesAsync(token, null, null, theme, ct);
        Changed?.Invoke();
    }

    public async Task SetReducedMotionAsync(bool reduced, CancellationToken ct = default) {
        _motion = reduced;
        cookies.Set(ViewerCookieOptions.MotionCookie, reduced ? "1" : "0");
        if (SessionToken() is { } token && api is not null) await api.SetPreferencesAsync(token, null, null, null, ct, reduced);
        Changed?.Invoke();
    }

    public async Task SetActiveEidAsync(string? eid, string? label = null, CancellationToken ct = default) {
        var trimmed = string.IsNullOrWhiteSpace(eid) ? null : eid.Trim();
        if (trimmed?.Length > ProfileGame.MaxEidLength) throw new ArgumentException("EID too long.", nameof(eid));
        _game = new ProfileGame { ActiveEid = trimmed, ActiveEidLabel = trimmed is null ? null : label?.Trim() };
        if (trimmed is null) cookies.Remove(ViewerCookieOptions.EidCookie);
        else cookies.Set(ViewerCookieOptions.EidCookie, trimmed);
        if (SessionToken() is { } token && api is not null) await api.SetGameAsync(token, _game, ct);
        Changed?.Invoke();
    }

    public async Task<ProfileResponse?> GetProfileAsync(CancellationToken ct = default) =>
        SessionToken() is { } token && api is not null ? await api.GetProfileAsync(token, ct) : null;

    public async Task<bool> SetLocaleAsync(string? timezone, string? language, CancellationToken ct = default) =>
        SessionToken() is { } token && api is not null && await api.SetPreferencesAsync(token, timezone, language, null, ct);

    public async Task<bool> SelectAvatarAsync(string provider, string subject, CancellationToken ct = default) =>
        SessionToken() is { } token && api is not null && await api.SelectAvatarAsync(token, provider, subject, ct);

    public async Task<bool> UnlinkIdentityAsync(string provider, string subject, CancellationToken ct = default) =>
        SessionToken() is { } token && api is not null && await api.UnlinkIdentityAsync(token, provider, subject, ct);

    public async Task<bool> SyncIdentitiesAsync(CancellationToken ct = default) =>
        SessionToken() is { } token && api is not null && await api.SyncIdentitiesAsync(token, ct);

    private string? SessionToken() => Viewer.IsAuthenticated ? _token : null;

    private static string? ReadToken(IHttpContextAccessor? accessor, SessionCookieOptions? session) {
        var name = session?.CookieName ?? DefaultSessionCookie;
        return accessor?.HttpContext?.Request.Cookies.TryGetValue(name, out var token) == true ? token : null;
    }
}

public static class SuiteServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentitySuite(this IServiceCollection services, string currentSlug, Action<SuiteOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSlug);
        var options = new SuiteOptions { CurrentSlug = currentSlug };
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddEggIdentityCurrentUser();
        services.AddEggIdentityUploads();
        services.TryAddScoped<ToastService>();
        var cookies = ViewerCookieOptions.In(services);
        cookies.Domain ??= $".{SuiteCatalog.HubHost}";
        foreach (var name in ViewerCookieOptions.SuiteCookies)
            if (!cookies.Cookies.ContainsKey(name)) cookies.Allow(name, TimeSpan.FromDays(365));
        services.AddScoped(sp => new SuiteContext(
            sp.GetRequiredService<SuiteOptions>(),
            sp.GetRequiredService<ICurrentUser>(),
            sp.GetRequiredService<ViewerCookies>(),
            sp.GetService<IdentityApiClient>(),
            sp.GetService<IHttpContextAccessor>(),
            sp.GetService<SessionCookieOptions>()));
        return services;
    }
}
