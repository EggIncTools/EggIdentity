using Microsoft.AspNetCore.Http;

namespace EggIdentity.Visits;

public static class VisitRequest {
    public const string ConsentCookie = "eggidentity_consent";
    public const int MaxPathLength = 512;
    private static readonly string[] BotMarkers = ["bot", "crawl", "spider", "headless", "preview"];

    public static bool IsExcluded(HttpRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers["DNT"].ToString().Trim() == "1") return true;
        if (request.Headers["Sec-GPC"].ToString().Trim() == "1") return true;
        if (request.Headers["Sec-Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)) return true;
        if (request.Headers["Sec-Fetch-Dest"].ToString() is { Length: > 0 } dest && dest is not ("document" or "empty")) return true;
        var ua = request.Headers.UserAgent.ToString();
        return Array.Exists(BotMarkers, m => ua.Contains(m, StringComparison.OrdinalIgnoreCase))
            || (request.Cookies.TryGetValue(ConsentCookie, out var consent) && AnalyticsDeclined(consent));
    }

    public static bool AnalyticsDeclined(string? cookie) {
        if (string.IsNullOrEmpty(cookie)) return false;
        var raw = Uri.UnescapeDataString(cookie);
        return Array.Exists(raw.Split('&'), pair => pair.Trim() == "a=0");
    }

    public static string? VisitorOf(HttpContext ctx, VisitsOptions options, VisitorHasher hasher) {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hasher);
        return IsExcluded(ctx.Request)
            ? null
            : hasher.Hash(options.Site, ClientIp.Resolve(ctx, options.HostedBehindProxy), ctx.Request.Headers.UserAgent.ToString());
    }

    public static string PathOf(string uri) {
        var path = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.AbsolutePath : "/";
        return path.Length > MaxPathLength ? path[..MaxPathLength] : path;
    }
}
