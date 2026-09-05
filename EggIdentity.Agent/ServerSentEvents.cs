using System.Globalization;
using System.Text.Json;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Agent;

public static class ServerSentEvents {
    public const string ContentType = "text/event-stream";
    public const string Keepalive = ": keepalive\n\n";
    public static readonly TimeSpan KeepaliveInterval = TimeSpan.FromSeconds(15);

    public static string Format(DeployEvent evt) {
        ArgumentNullException.ThrowIfNull(evt);
        return $"id: {evt.Id.ToString(CultureInfo.InvariantCulture)}\nevent: deploy\ndata: {JsonSerializer.Serialize(evt)}\n\n";
    }

    public static long ResolveAfter(string? lastEventIdHeader, string? afterQuery) {
        if (TryParse(lastEventIdHeader, out var fromHeader)) return fromHeader;
        if (TryParse(afterQuery, out var fromQuery)) return fromQuery;
        return 0;
    }

    private static bool TryParse(string? text, out long value) {
        value = 0;
        return !string.IsNullOrWhiteSpace(text)
            && long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    public static async Task StreamAsync(HttpResponse response, DeployEventRing ring, long after, TimeSpan keepaliveInterval, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(ring);

        response.ContentType = ContentType;
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = ring.Subscribe();
        var lastSent = after;
        try {
            foreach (var evt in ring.Since(after)) {
                await response.WriteAsync(Format(evt), ct);
                lastSent = evt.Id;
            }
            await response.Body.FlushAsync(ct);

            while (!ct.IsCancellationRequested) {
                using var keepalive = CancellationTokenSource.CreateLinkedTokenSource(ct);
                keepalive.CancelAfter(keepaliveInterval);
                try {
                    if (!await subscription.Reader.WaitToReadAsync(keepalive.Token)) return;
                    while (subscription.Reader.TryRead(out var evt)) {
                        if (evt.Id <= lastSent) continue;
                        await response.WriteAsync(Format(evt), ct);
                        lastSent = evt.Id;
                    }
                } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
                    await response.WriteAsync(Keepalive, ct);
                }
                await response.Body.FlushAsync(ct);
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
        }
    }
}
