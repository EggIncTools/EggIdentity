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
