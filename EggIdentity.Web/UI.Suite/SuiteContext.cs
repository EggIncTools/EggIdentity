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
    SessionCookieOptions? session = null,
    ISuiteEidStore? eidStore = null) {
    private readonly string? _token = ReadToken(accessor, session);
    private readonly ISuiteEidStore _eids = eidStore ?? new IdentityEidStore(user, api, accessor, session);
    private bool _loaded;
    private bool? _motion;
    private string? _theme;
    private ProfileGame? _game;

    public event Action? Changed;

    public event Action<string>? EidRemoved;

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

    public IReadOnlyList<ProfileEid> Eids => _game?.Eids ?? [];

    public ProfileEid? ActiveAccount => ActiveEid is { } eid ? Eids.FirstOrDefault(e => e.Eid == eid) : null;

    public bool CanEditEids => _eids.IsAvailable;

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
        try {
            if (SessionToken() is { } token && api is not null) {
                var profile = await api.GetProfileAsync(token, ct);
                _theme = profile?.Theme ?? _theme;
                _motion = profile?.ReducedMotion ?? _motion;
            }
            if (_eids.IsAvailable) _game = await _eids.LoadAsync(ct);
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
        var activeLabel = trimmed is null ? null : label?.Trim();
        var list = Eids;
        if (trimmed is not null && list.All(e => e.Eid != trimmed) && EidRules.IsValid(trimmed) && _eids.IsAvailable)
            list = [.. list, new ProfileEid { Eid = trimmed, Label = EidRules.NormalizeLabel(activeLabel) }];
        _game = new ProfileGame { ActiveEid = trimmed, ActiveEidLabel = activeLabel, Eids = list };
        if (trimmed is null) cookies.Remove(ViewerCookieOptions.EidCookie);
        else cookies.Set(ViewerCookieOptions.EidCookie, trimmed);
        if (_eids.IsAvailable) await _eids.SetActiveAsync(trimmed, activeLabel, ct);
        Changed?.Invoke();
    }

    public Task<EidUpsertResult?> AddEidAsync(string eid, string? label = null, EidSnapshot? snapshot = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(eid);
        return UpsertEidsAsync([new EidUpsert { Eid = eid.Trim(), Label = label, Snapshot = snapshot }], ct);
    }

    public async Task<EidUpsertResult?> UpsertEidsAsync(IReadOnlyList<EidUpsert> entries, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(entries);
        if (!_eids.IsAvailable) return null;
        var result = await _eids.UpsertAsync(entries, ct);
        if (result is not null) ReplaceEids(result.Eids);
        return result;
    }

    public async Task<bool> UpdateEidSnapshotAsync(string eid, EidSnapshot snapshot, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!_eids.IsAvailable || !await _eids.UpdateSnapshotAsync(eid, snapshot, ct)) return false;
        ReplaceEids([.. Eids.Select(e => e.Eid == eid ? Copy(e, e.Label, snapshot) : e)]);
        return true;
    }

    public async Task<bool> RenameEidAsync(string eid, string? label, CancellationToken ct = default) {
        if (!_eids.IsAvailable || !await _eids.RenameAsync(eid, label, ct)) return false;
        ReplaceEids([.. Eids.Select(e => e.Eid == eid ? Copy(e, EidRules.NormalizeLabel(label), e.Snapshot) : e)]);
        return true;
    }

    public async Task<bool> RemoveEidAsync(string eid, CancellationToken ct = default) {
        if (!_eids.IsAvailable || !await _eids.RemoveAsync(eid, ct)) return false;
        var active = ActiveEid == eid ? null : _game?.ActiveEid;
        if (active is null && ActiveEid == eid) cookies.Remove(ViewerCookieOptions.EidCookie);
        _game = new ProfileGame {
            ActiveEid = active,
            ActiveEidLabel = active is null ? null : _game?.ActiveEidLabel,
            Eids = [.. Eids.Where(e => e.Eid != eid)],
        };
        EidRemoved?.Invoke(eid);
        Changed?.Invoke();
        return true;
    }

    public async Task<bool> ReorderEidsAsync(IReadOnlyList<string> eids, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(eids);
        if (!_eids.IsAvailable || !await _eids.ReorderAsync(eids, ct)) return false;
        var byId = Eids.ToDictionary(e => e.Eid, StringComparer.Ordinal);
        ReplaceEids([.. eids.Where(byId.ContainsKey).Select(id => byId[id])]);
        return true;
    }

    private void ReplaceEids(IReadOnlyList<ProfileEid> eids) {
        _game = new ProfileGame { ActiveEid = _game?.ActiveEid, ActiveEidLabel = _game?.ActiveEidLabel, Eids = eids };
        Changed?.Invoke();
    }

    private static ProfileEid Copy(ProfileEid entry, string? label, EidSnapshot? snapshot) =>
        new() { Eid = entry.Eid, Label = label, Snapshot = snapshot, SnapshotAt = snapshot == entry.Snapshot ? entry.SnapshotAt : DateTimeOffset.UtcNow };

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
        services.TryAddScoped<ISuiteEidStore>(sp => new IdentityEidStore(
            sp.GetRequiredService<ICurrentUser>(),
            sp.GetService<IdentityApiClient>(),
            sp.GetService<IHttpContextAccessor>(),
            sp.GetService<SessionCookieOptions>()));
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
            sp.GetService<SessionCookieOptions>(),
            sp.GetRequiredService<ISuiteEidStore>()));
        return services;
    }
}
