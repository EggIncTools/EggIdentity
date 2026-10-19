using System.Security.Claims;

namespace EggIdentity.Reports.Browser;

public sealed record ReportHooks(
    Func<ReportQuery, ReportQuery>? Prepare = null,
    Func<ReportCube, ReportQuery, CancellationToken, Task<ReportCube>>? PostProcess = null);

public sealed class BrowserReportOptions {
    public string RoutePrefix { get; set; } = "/_reports";

    public Func<IServiceProvider, ClaimsPrincipal, ReportTier> Tier { get; set; } = (_, _) => ReportTier.Live;

    public Func<ClaimsPrincipal, string?> ScopeKey { get; set; } = principal => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public HashSet<string> SharedSources { get; } = [];

    public long MaxCacheBytes { get; set; } = 256L * 1024 * 1024;

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RevalidateAfter { get; set; } = TimeSpan.FromSeconds(15);

    public long MaxBrowserCacheBytes { get; set; } = 512L * 1024 * 1024;

    public Func<ReportQuery, ReportQuery>? Prepare { get; set; }

    public Dictionary<string, ReportHooks> Hooks { get; } = [];
}
