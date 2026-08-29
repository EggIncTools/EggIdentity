using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using EggIdentity.Contract;

namespace EggIdentity.Client.Tests;

public class IdentityApiClientTests {
    private static (IdentityApiClient client, StubHttpMessageHandler handler) MakeClient(HttpResponseMessage response) {
        var handler = new StubHttpMessageHandler(_ => response);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://identity.internal") };
        http.DefaultRequestHeaders.Add("Authorization", "Bearer test-secret");
        return (new IdentityApiClient(http), handler);
    }

    [Fact]
    public async Task ResolveAsync_PostsRequestBody_AndParsesResponse() {
        var expected = new IdentityResolveResponse { UserId = Guid.NewGuid(), Role = "viewer", DiscordId = "d1", IsNew = true };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.ResolveAsync("discord", "d1", "d1", "alice", null, CancellationToken.None);

        Assert.Equal(expected.UserId, result.UserId);
        Assert.Equal("viewer", result.Role);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/identity/resolve", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"provider\":\"discord\"", handler.LastRequestBody);
        Assert.Equal("Bearer test-secret", handler.LastRequest.Headers.Authorization?.ToString().Replace("Bearer ", "Bearer "));
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull() {
        var (client, _) = MakeClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSponsorStatusAsync_ParsesResponse() {
        var userId = Guid.NewGuid();
        var expected = new SponsorStatusResponse { IsSponsor = true, LastSyncedAt = DateTimeOffset.UtcNow };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.GetSponsorStatusAsync(userId, CancellationToken.None);

        Assert.Equal(expected.IsSponsor, result.IsSponsor);
        Assert.Equal(expected.LastSyncedAt, result.LastSyncedAt);
        Assert.Equal($"/identity/{userId}/sponsor", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    [Fact]
    public async Task ListAdminUsersAsync_ParsesResponse() {
        var expected = new List<IdentityUserResponse> {
            new() { UserId = Guid.NewGuid(), Username = "alice", Role = "admin" },
            new() { UserId = Guid.NewGuid(), Username = "bob", Role = "viewer" },
        };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.ListAdminUsersAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal("/identity/admin/users", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    [Fact]
    public async Task SetRoleAsync_PostsRoleBody() {
        var userId = Guid.NewGuid();
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetRoleAsync(userId, "admin", CancellationToken.None);

        Assert.Equal($"/identity/{userId}/role", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("\"role\":\"admin\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetLoginSourcesAsync_ParsesResponse_AndBuildsQuery() {
        var expected = new LoginSourcesResponse {
            Sources = [new LoginSourceResponse { Name = "Discord", IconUrl = "/auth/icons/discord", Url = "/auth/go/discord" }],
        };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.GetLoginSourcesAsync("https://app.example.com/return", "modal", CancellationToken.None);

        Assert.Single(result.Sources);
        Assert.Equal("Discord", result.Sources[0].Name);
        Assert.Equal("/auth/sources", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("mode=modal", handler.LastRequest.RequestUri.Query);
        Assert.Contains("https%3A%2F%2Fapp.example.com%2Freturn", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public void IconUrl_BuildsRelativeUrl_WithProvider() {
        var (client, _) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK));

        var url = client.IconUrl("discord");

        Assert.Equal("/auth/icons/discord", url);
    }

    [Fact]
    public async Task UploadAvatarAsync_PostsFile_AndReturnsSuccess() {
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.NoContent));
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await client.UploadAvatarAsync("tok-1", stream, "avatar.png", "image/png", CancellationToken.None);

        Assert.True(result);
        Assert.Equal("/profile/avatar", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
    }

    [Fact]
    public async Task RevokeSessionAsync_PostsSidBody() {
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.RevokeSessionAsync("sid-9", CancellationToken.None);

        Assert.Equal("/identity/revoke-session", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("\"sid\":\"sid-9\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task IsRevokedAsync_ParsesBoolBody() {
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(true) });

        var result = await client.IsRevokedAsync("sid-1", CancellationToken.None);

        Assert.True(result);
        Assert.Equal("/identity/sessions/sid-1/revoked", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MergeAsync_ReturnsWinnerUserId() {
        var keepId = Guid.NewGuid();
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { userId = keepId }) });

        var result = await client.MergeAsync(keepId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(keepId, result);
        Assert.Equal("/identity/merge", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RedeemAsync_PostsCodeAndReturnsResponse() {
        var expected = new RedeemLoginCodeResponse { UserId = Guid.NewGuid(), Role = "viewer", Username = "alice", IsNew = false };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.RedeemAsync("abc123", CancellationToken.None);

        Assert.Equal(expected.UserId, result.UserId);
        Assert.Equal("alice", result.Username);
        Assert.Equal("/identity/redeem", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"code\":\"abc123\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetProfileAsync_SetsSessionHeader_AndParsesResponse() {
        var expected = new ProfileResponse { UserId = Guid.NewGuid(), Username = "alice", Identities = [] };
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });

        var result = await client.GetProfileAsync("tok-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("alice", result!.Username);
        Assert.Equal("/profile/me", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("tok-1", handler.LastRequest.Headers.GetValues("X-EggIdentity-Session").Single());
    }

    [Fact]
    public async Task GetProfileAsync_Unauthorized_ReturnsNull() {
        var (client, _) = MakeClient(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await client.GetProfileAsync("tok-1", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void StartLinkUrl_BuildsRelativeUrl_WithProviderAndReturnUrl() {
        var (client, _) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK));

        var url = client.StartLinkUrl("github", "https://app.example.com/settings");

        Assert.Equal("/profile/link/github/start?returnUrl=https%3A%2F%2Fapp.example.com%2Fsettings", url);
    }

    [Fact]
    public void StartRelinkUrl_BuildsRelativeUrl_WithProviderAndReturnUrl() {
        var (client, _) = MakeClient(new HttpResponseMessage(HttpStatusCode.OK));

        var url = client.StartRelinkUrl("github", "https://app.example.com/settings");

        Assert.Equal("/auth/relink/github?returnUrl=https%3A%2F%2Fapp.example.com%2Fsettings", url);
    }

    [Fact]
    public async Task UnlinkIdentityAsync_PostsToUnlinkRoute() {
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await client.UnlinkIdentityAsync("tok-1", "discord", "d1", CancellationToken.None);

        Assert.True(result);
        Assert.Equal("/profile/identities/discord/d1/unlink", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SelectAvatarAsync_PostsProviderAndSubject() {
        var (client, handler) = MakeClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await client.SelectAvatarAsync("tok-1", "authentik", "sub-1", CancellationToken.None);

        Assert.True(result);
        Assert.Equal("/profile/avatar/select", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"provider\":\"authentik\"", handler.LastRequestBody);
    }

    [Fact]
    public void PublicSurface_MatchesExpectedMethodSet() {
        var expected = new HashSet<string> {
            "ResolveAsync", "GetAsync", "ListAdminUsersAsync", "RevokeSessionAsync", "IsRevokedAsync",
            "MergeAsync", "SetRoleAsync", "RedeemAsync", "GetLoginSourcesAsync", "GetProfileAsync",
            "StartLinkUrl", "StartRelinkUrl", "IconUrl", "UnlinkIdentityAsync", "UploadAvatarAsync",
            "SelectAvatarAsync", "GetSponsorStatusAsync", "GetSupporterStatusAsync", "RefreshSupporterStatusAsync",
            "SetPreferencesAsync",
        };
        var actual = typeof(IdentityApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();
        Assert.Equal(expected, actual);
    }
}
