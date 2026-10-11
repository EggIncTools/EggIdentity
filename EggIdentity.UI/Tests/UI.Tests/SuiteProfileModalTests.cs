using System.Net;
using System.Text.Json;
using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Contract;
using EggIdentity.Testing;
using EggIdentity.UI.Suite;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Tests;

public class SuiteProfileModalTests {
    private const string Discord = "discord";
    private const string AliceDiscord = "alice#1";

    private sealed class TestNav : NavigationManager {
        public TestNav() => Initialize("https://eggledger.egginc.tools/", "https://eggledger.egginc.tools/missions");

        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class FixedUser(CurrentUser user) : ICurrentUser {
        public CurrentUser Current => user;

        public Task<CurrentUser> GetAsync() => Task.FromResult(user);
    }

    private static ProfileIdentityResponse Identity(string provider, string name) =>
        new() { Provider = provider, Subject = provider + "-1", Username = name, Avatar = $"https://cdn.example/{provider}.png" };

    private static async Task<string> RenderAsync<T>(bool signedIn, IReadOnlyList<ProfileIdentityResponse> identities, Dictionary<string, object?> parameters)
        where T : IComponent {
        var profile = new ProfileResponse { UserId = Guid.NewGuid(), Username = "alice", Identities = [.. identities], Timezone = "UTC" };
        var stub = new StubHttpMessageHandler(req => req.RequestUri!.AbsolutePath switch {
            "/profile/me" => Json(profile),
            "/profile/game" => Json(new ProfileGame { ActiveEid = "EI123", ActiveEidLabel = "main" }),
            "/suite/tools" => Json(new List<SuiteTool>()),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var http = new DefaultHttpContext();
        if (signedIn) http.Request.Headers.Cookie = "eggidentity_session=tok";
        var user = signedIn ? new FakeUser(Guid.NewGuid(), Name: "alice").Current() : CurrentUser.Anonymous;

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(new FixedUser(user));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddSingleton(new IdentityApiClient(stub.Client()));
        services.AddSingleton(new SuiteOptions { CurrentSlug = "ledger" });
        services.AddSingleton(new ViewerCookieOptions());
        services.AddScoped<ViewerCookies>();
        services.AddScoped(sp => new SuiteContext(
            sp.GetRequiredService<SuiteOptions>(), sp.GetRequiredService<ICurrentUser>(), sp.GetRequiredService<ViewerCookies>(),
            sp.GetRequiredService<IdentityApiClient>(), sp.GetRequiredService<IHttpContextAccessor>()));
        services.AddScoped<ToastService>();
        services.AddEggIdentityUploads();
        services.AddSingleton<NavigationManager, TestNav>();
        services.AddSingleton<INavigationInterception>(new NoInterception());
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private sealed class NoInterception : INavigationInterception {
        public Task EnableNavigationInterceptionAsync() => Task.CompletedTask;
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json") };

    private static Dictionary<string, object?> OpenModal => new() { [nameof(SuiteProfileModal.Open)] = true };

    [Fact]
    public async Task AnonymousShowsProviderSignInInsteadOfProfile() {
        var html = await RenderAsync<SuiteProfileModal>(false, [], OpenModal);

        Assert.Contains("suite-profile-providers", html);
        Assert.Contains("/auth/go/github", html);
        Assert.DoesNotContain("suite-profile-identities", html);
    }

    [Fact]
    public async Task SingleIdentityCannotBeUnlinked() {
        var html = await RenderAsync<SuiteProfileModal>(true, [Identity(Discord, AliceDiscord)], OpenModal);

        Assert.Contains(AliceDiscord, html);
        Assert.Contains(SuiteProfileModal.LastIdentityReason, html);
        Assert.Contains("disabled", html);
    }

    [Fact]
    public async Task SeveralIdentitiesAreAllUnlinkable() {
        var html = await RenderAsync<SuiteProfileModal>(true, [Identity(Discord, AliceDiscord), Identity("github", "alice-gh")], OpenModal);

        Assert.Contains("alice-gh", html);
        Assert.DoesNotContain(SuiteProfileModal.LastIdentityReason, html);
        Assert.Contains("value=\"EI123\"", html);
    }

    [Fact]
    public async Task ClosedModalRendersNothing() {
        var html = await RenderAsync<SuiteProfileModal>(true, [Identity(Discord, AliceDiscord)], []);
        Assert.DoesNotContain("suite-profile-body", html);
    }

    [Fact]
    public void ProfileButtonClosesTheMenuAndOpensTheModal() {
        var account = new SuiteAccount();
        var menu = typeof(SuiteAccount).GetField("_menuOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var modal = typeof(SuiteAccount).GetField("_profileOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        menu.SetValue(account, true);

        account.OpenProfile();

        Assert.False((bool)menu.GetValue(account)!);
        Assert.True((bool)modal.GetValue(account)!);
    }

    [Fact]
    public async Task AccountMenuOffersAProfileButtonNotALink() {
        var html = await RenderAsync<SuiteAccount>(true, [Identity(Discord, AliceDiscord)], []);

        Assert.Contains("<button type=\"button\" class=\"suite-account-link suite-profile-open\"", html);
        Assert.DoesNotContain("/profile\"", html);
        Assert.DoesNotContain("suite-profile-body", html);
    }
}
