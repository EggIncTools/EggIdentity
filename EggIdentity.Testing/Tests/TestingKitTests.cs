using System.Net;
using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Testing.Tests;

public class TestingKitTests {
    [Fact]
    public void FakeUser_RoundTripsThroughTheSessionClaims() {
        var fake = new FakeUser(Guid.NewGuid(), UserRole.Contributor, "Ada", "42", "https://a.test/a.png", true);

        var user = fake.Current();

        Assert.Equal(new CurrentUser(fake.Id, "42", "Ada", "https://a.test/a.png", UserRole.Contributor, true, true), user);
        Assert.True(user.IsAtLeast(UserRole.Contributor));
        Assert.False(user.IsAtLeast(UserRole.Admin));
        Assert.Equal(user, fake.Accessor().Current);
    }

    [Fact]
    public async Task Accessor_ResolvesTheSameUserAsync() {
        var fake = FakeUser.Admin();

        Assert.Equal(fake.Current(), await fake.Accessor().GetAsync());
    }

    [Theory]
    [InlineData("/avatars/1", "https://id.test/", "https://id.test/avatars/1")]
    [InlineData("/avatars/1", "https://id.test", "https://id.test/avatars/1")]
    [InlineData("/avatars/1", null, "/avatars/1")]
    [InlineData("https://cdn.test/a.png", "https://id.test", "https://cdn.test/a.png")]
    [InlineData("//cdn.test/a.png", "https://id.test", "//cdn.test/a.png")]
    [InlineData(null, "https://id.test", null)]
    public void AvatarUrl_ResolvesRootRelativeAgainstIdentityHost(string? avatar, string? host, string? expected) {
        var user = new FakeUser(Guid.NewGuid(), Avatar: avatar).Current();

        Assert.Equal(expected, user.AvatarUrl(host));
    }

    [Fact]
    public void Anonymous_IsNotAuthenticatedAndHasNoRole() {
        var user = FakeUser.Anonymous().ToCurrentUser();

        Assert.Same(CurrentUser.Anonymous, user);
        Assert.False(user.IsAtLeast(UserRole.Viewer));
    }

    [Fact]
    public async Task StubFactory_RoutesNamedClientsThroughOneHandler() {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "pong");
        var factory = new StubHttpFactory(handler).WithBase("api", "https://api.test/");

        var body = await factory.CreateClient("api").GetStringAsync("ping");

        Assert.Equal("pong", body);
        Assert.Equal(new Uri("https://api.test/ping"), handler.LastRequest?.RequestUri);
        Assert.Single(handler.Requests);
        Assert.Equal("api", factory.LastName);
    }

    [Fact]
    public async Task StubFactory_WithoutHandlerAnswersOk() {
        var factory = new StubHttpFactory();

        using var res = await factory.CreateClient("egress").GetAsync("x");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(["egress"], factory.Names);
    }

    [Fact]
    public void TempDir_CombinesSegmentsAndCreatesSubdirs() {
        using var dir = new TempDir();

        var sub = dir.CreateSubdir("a", "b");

        Assert.True(Directory.Exists(sub));
        Assert.Equal(Path.Combine(dir.Path, "a", "b"), dir.File("a", "b"));
        Assert.True(Directory.Exists(dir.CreateSubdir()));
    }

    [Fact]
    public async Task StubResponses_CarryStatusBodyAndContentType() {
        using var raw = StubResponses.Json(HttpStatusCode.BadRequest, "{not json");
        using var typed = StubResponses.Json(HttpStatusCode.OK, new { UserId = 7 });
        using var bytes = StubResponses.Bytes(HttpStatusCode.OK, [1, 2, 3], "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, raw.StatusCode);
        Assert.Equal("{not json", await raw.Content.ReadAsStringAsync());
        Assert.Equal("application/json", raw.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"userId\":7}", await typed.Content.ReadAsStringAsync());
        Assert.Equal([1, 2, 3], await bytes.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", bytes.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void TempDir_IsRemovedOnDispose() {
        string path;
        using (var dir = new TempDir()) {
            path = dir.Path;
            dir.Write("a/b.txt", "x");
            Assert.True(File.Exists(dir.File("a/b.txt")));
        }
        Assert.False(Directory.Exists(path));
    }
}
