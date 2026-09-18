namespace EggIdentity.Host.Tests;

public class AvatarResolverTests {
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public void TryParseDataUri_ImagePngBase64_DecodesBytesAndMediaType() {
        var uri = "data:image/png;base64," + Convert.ToBase64String(PngHeader);

        var ok = AvatarResolver.TryParseDataUri(uri, out var bytes, out var mediaType);

        Assert.True(ok);
        Assert.Equal(PngHeader, bytes);
        Assert.Equal("image/png", mediaType);
    }

    [Fact]
    public void TryParseDataUri_BadBase64_ReturnsFalse() {
        var ok = AvatarResolver.TryParseDataUri("data:image/png;base64,not*base64!", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseDataUri_NonImageMediaType_ReturnsFalse() {
        var uri = "data:text/html;base64," + Convert.ToBase64String("<script>"u8.ToArray());

        var ok = AvatarResolver.TryParseDataUri(uri, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseDataUri_NotBase64Encoded_ReturnsFalse() {
        var ok = AvatarResolver.TryParseDataUri("data:image/svg+xml,<svg/>", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ComputeETag_SameValue_IsStable() {
        var first = AvatarResolver.ComputeETag("data:image/png;base64,AAAA");
        var second = AvatarResolver.ComputeETag("data:image/png;base64,AAAA");

        Assert.Equal(first, second);
        Assert.StartsWith("\"", first);
        Assert.EndsWith("\"", first);
    }

    [Fact]
    public void ComputeETag_DifferentValueOrFileState_Changes() {
        var a = AvatarResolver.ComputeETag("data:image/png;base64,AAAA");
        var b = AvatarResolver.ComputeETag("data:image/png;base64,BBBB");
        var stamp = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc);
        var file1 = AvatarResolver.ComputeETag("/tmp/x.png", 10, stamp);
        var file2 = AvatarResolver.ComputeETag("/tmp/x.png", 11, stamp);
        var file3 = AvatarResolver.ComputeETag("/tmp/x.png", 10, stamp.AddSeconds(1));

        Assert.NotEqual(a, b);
        Assert.NotEqual(file1, file2);
        Assert.NotEqual(file1, file3);
        Assert.Equal(file1, AvatarResolver.ComputeETag("/tmp/x.png", 10, stamp));
    }

    [Fact]
    public void IfNoneMatchHits_MatchesExactWeakAndStar() {
        var etag = AvatarResolver.ComputeETag("v");

        Assert.True(AvatarResolver.IfNoneMatchHits(etag, etag));
        Assert.True(AvatarResolver.IfNoneMatchHits("W/" + etag, etag));
        Assert.True(AvatarResolver.IfNoneMatchHits("\"other\", " + etag, etag));
        Assert.True(AvatarResolver.IfNoneMatchHits("*", etag));
        Assert.False(AvatarResolver.IfNoneMatchHits("\"other\"", etag));
        Assert.False(AvatarResolver.IfNoneMatchHits(null, etag));
    }
}
