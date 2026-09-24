using Microsoft.AspNetCore.Http;

namespace EggIdentity.Metrics;

public enum RequestBucket { Internal = 0, Cross = 1, External = 2 }

public sealed class RequestBucketClassifier(RequestMetricsOptions options) {
    public RequestBucket Classify(HttpContext ctx, System.Security.Claims.ClaimsPrincipal? user) {
        if (!string.IsNullOrEmpty(options.InternalMarkerHeader)
            && ctx.Request.Headers.ContainsKey(options.InternalMarkerHeader)) {
            return RequestBucket.Internal;
        }
        return user?.Identity?.IsAuthenticated == true ? RequestBucket.Cross : RequestBucket.External;
    }

    public static string ToName(RequestBucket bucket) => bucket.ToString().ToLowerInvariant();
}
