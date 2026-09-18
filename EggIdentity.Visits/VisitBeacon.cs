using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Visits;

public sealed record VisitBeat(string? Path, double Seconds);

public static class VisitBeacon {
    public const string ConsentCookie = "eggidentity_consent";
    private const int MaxPathLength = 512;
    private static readonly string[] BotMarkers = ["bot", "crawl", "spider", "headless", "preview"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool IsExcluded(HttpRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Headers["DNT"].ToString().Trim() == "1") return true;
        if (request.Headers["Sec-GPC"].ToString().Trim() == "1") return true;
        var ua = request.Headers.UserAgent.ToString();
        if (Array.Exists(BotMarkers, m => ua.Contains(m, StringComparison.OrdinalIgnoreCase))) return true;
        return request.Cookies.TryGetValue(ConsentCookie, out var consent) && AnalyticsDeclined(consent);
    }

    public static bool AnalyticsDeclined(string? cookie) {
        if (string.IsNullOrEmpty(cookie)) return false;
        var raw = Uri.UnescapeDataString(cookie);
        return Array.Exists(raw.Split('&'), pair => pair.Trim() == "a=0");
    }

    public static async Task<IResult> HandleAsync(HttpContext ctx, VisitsOptions options, VisitorHasher hasher, VisitTracker tracker) {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(tracker);
        if (IsExcluded(ctx.Request)) return Results.NoContent();

        VisitBeat? beat;
        try {
            beat = await JsonSerializer.DeserializeAsync<VisitBeat>(ctx.Request.Body, Json, ctx.RequestAborted);
        } catch (JsonException) {
            return Results.BadRequest();
        }

        if (beat?.Path is not { Length: > 0 and <= MaxPathLength } path || path[0] != '/') return Results.BadRequest();
        if (double.IsNaN(beat.Seconds)) return Results.BadRequest();

        var seconds = (int)Math.Clamp(beat.Seconds, 0, options.SessionGap.TotalSeconds);
        var ip = ClientIp.Resolve(ctx, options.HostedBehindProxy);
        var ua = ctx.Request.Headers.UserAgent.ToString();
        tracker.Record(hasher.Hash(options.Site, ip, ua), path, seconds);
        return Results.NoContent();
    }
}
