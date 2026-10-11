using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EggIdentity.Client;
using EggIdentity.Contract;
using EggIdentity.Resilience;

namespace EggIdentity.UI.Suite;

public sealed record SuiteEntry(string Slug, string Url, bool Live, int Order) {
    public string Name => Brands.Find(Slug)?.DisplayName ?? Slug;

    public string Accent => Brands.Find(Slug)?.Accent ?? "";

    public string IconUrl(int size = 64) => Brands.IconPath(Slug, Brands.NearestIconSize(size));

    public string MarkStyle => SuiteCatalog.MarkStyleFor(Slug);

    public string ToolStyle => string.IsNullOrEmpty(Accent) ? "" : $"--tool: {Accent}";
}

public static class SuiteCatalog {
    public const string HubHost = "egginc.tools";
    public const string HubUrl = $"https://{HubHost}";
    public const string IdentityOriginEnv = "IDENTITY_WIDGET_URL";
    public const string DefaultIdentityOrigin = $"https://id.{HubHost}";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
    private static readonly Lock Gate = new();
    private static TtlSnapshot<IReadOnlyList<SuiteEntry>>? _snapshot;

    public static IReadOnlyList<string> ReturnHosts { get; } = [HubHost];

    public static Action<string>? OnReturnRejected { get; set; }

    public static string IdentityOrigin { get; } = ResolveIdentityOrigin();

    public static string SignOutUrl => $"{IdentityOrigin}/auth/logout";

    public static IReadOnlyList<SuiteEntry> Fallback { get; } = [
        new("ledger", "https://eggledger.egginc.tools", true, 0),
        new("incognito", "https://eggincognito.egginc.tools", true, 1),
        new("abacus", "https://eggabacus.egginc.tools", false, 2),
    ];

    public static IReadOnlyList<string> Providers { get; } = ["discord", "google", "microsoft", "github"];

    private const double BaseGutter = 0.078125;

    private static readonly FrozenDictionary<string, double> Gutters = new Dictionary<string, double> {
        ["ledger"] = 0.15625,
        ["incognito"] = 0.21875,
        ["abacus"] = 0.09375,
        ["identity"] = 0.0546875,
        ["tools"] = BaseGutter,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<SuiteEntry>> LoadAsync(IdentityApiClient? api, CancellationToken ct = default) {
        if (api is null) return Fallback;
        TtlSnapshot<IReadOnlyList<SuiteEntry>> snapshot;
        lock (Gate) {
            snapshot = _snapshot ??= new TtlSnapshot<IReadOnlyList<SuiteEntry>>(CacheTtl, token => FetchAsync(api, token));
        }
        try {
            return await snapshot.GetAsync(ct);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return Fallback;
        }
    }

    public static IReadOnlyList<SuiteEntry> FromWire(IReadOnlyList<SuiteTool> tools) {
        ArgumentNullException.ThrowIfNull(tools);
        var entries = tools
            .Where(t => Brands.Find(t.Slug) is not null && IsAllowedReturn(t.Url))
            .OrderBy(t => t.Order)
            .Select(t => new SuiteEntry(t.Slug, t.Url.TrimEnd('/'), t.Live, t.Order))
            .ToList();
        return entries.Count == 0 ? Fallback : entries;
    }

    public static string MarkStyleFor(string slug) {
        if (!Gutters.TryGetValue(slug, out var gutter) || gutter <= BaseGutter) return "";
        var scale = (1 - 2 * BaseGutter) / (1 - 2 * gutter);
        return string.Create(CultureInfo.InvariantCulture, $"scale: {scale:0.###}");
    }

    public static string ProviderIcon(string provider) => $"brand-{provider}";

    public static string ProviderSignInUrl(string provider, string returnUrl) =>
        $"{IdentityOrigin}/auth/go/{provider}?returnUrl={Uri.EscapeDataString(SafeReturn(returnUrl))}";

    public static string SignOutUrlFor(string returnUrl) =>
        $"{SignOutUrl}?returnUrl={Uri.EscapeDataString(SafeReturn(returnUrl))}";

    public static bool IsAllowedReturn([NotNullWhen(true)] string? returnUrl) =>
        Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed) && IsWebLoopbackOrHttps(parsed)
        && (parsed.IsLoopback || ReturnHosts.Any(host => IsHostOrSubdomain(parsed.Host, host)));

    private static bool IsWebLoopbackOrHttps(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback);

    private static async Task<IReadOnlyList<SuiteEntry>> FetchAsync(IdentityApiClient api, CancellationToken ct) =>
        FromWire(await api.GetSuiteToolsAsync(ct));

    private static bool IsHostOrSubdomain(string candidate, string parent) =>
        candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || candidate.EndsWith($".{parent}", StringComparison.OrdinalIgnoreCase);

    private static string SafeReturn(string returnUrl) {
        if (IsAllowedReturn(returnUrl)) return returnUrl;
        OnReturnRejected?.Invoke(Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed) ? parsed.Host : "(not an absolute url)");
        return HubUrl;
    }

    private static string ResolveIdentityOrigin() {
        var raw = Environment.GetEnvironmentVariable(IdentityOriginEnv)?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(raw)) return DefaultIdentityOrigin;
        return Uri.TryCreate(raw, UriKind.Absolute, out var parsed) && IsWebLoopbackOrHttps(parsed) ? raw : DefaultIdentityOrigin;
    }
}
