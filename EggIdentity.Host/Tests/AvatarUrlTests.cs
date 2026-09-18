using EggIdentity.Contract;

namespace EggIdentity.Host.Tests;

public class AvatarUrlTests {
    [Fact]
    public void Canonical_StoredValueOfAnyShape_ReturnsAvatarPath() {
        var userId = Guid.NewGuid();
        var expected = IdentityWire.AvatarPath(userId);

        Assert.Equal(expected, AvatarUrl.Canonical(userId, "data:image/png;base64,AAAA"));
        Assert.Equal(expected, AvatarUrl.Canonical(userId, "https://cdn.example.com/a.png"));
        Assert.Equal(expected, AvatarUrl.Canonical(userId, $"/avatars/{userId}"));
        Assert.Equal(expected, AvatarUrl.Canonical(userId, "abc123hash"));
        Assert.Equal($"/avatars/{userId}", expected);
    }

    [Fact]
    public void Canonical_NullOrEmpty_ReturnsNull() {
        var userId = Guid.NewGuid();

        Assert.Null(AvatarUrl.Canonical(userId, null));
        Assert.Null(AvatarUrl.Canonical(userId, ""));
    }
}
