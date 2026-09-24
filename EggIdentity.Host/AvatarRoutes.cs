namespace EggIdentity.Host;

public static class AvatarRoutes {
    public static void Map(WebApplication app, string avatarStorageDir, UserQueries users) {
        app.MapGet("/avatars/{userId:guid}", async (HttpContext ctx, Guid userId) => {
            if (AvatarStore.TryGetPath(avatarStorageDir, userId, out var path, out var contentType)) {
                var info = new FileInfo(path);
                var fileTag = AvatarResolver.ComputeETag(path, info.Length, info.LastWriteTimeUtc);
                return Serve(ctx, fileTag, () => Results.File(path, contentType, enableRangeProcessing: false));
            }

            var user = await users.GetAsync(userId, ctx.RequestAborted);
            var stored = user?.Avatar;
            if (string.IsNullOrEmpty(stored)) return Results.NotFound();

            if (stored.StartsWith("data:", StringComparison.Ordinal)) {
                return AvatarResolver.TryParseDataUri(stored, out var bytes, out var mediaType)
                    ? Serve(ctx, AvatarResolver.ComputeETag(stored), () => Results.Bytes(bytes, mediaType))
                    : Results.NotFound();
            }

            return stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? Results.Redirect(stored, permanent: false)
                : Results.NotFound();
        });
    }

    private static IResult Serve(HttpContext ctx, string etag, Func<IResult> body) {
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.ETag = etag;
        return AvatarResolver.IfNoneMatchHits(ctx.Request.Headers.IfNoneMatch.ToString(), etag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : body();
    }
}
