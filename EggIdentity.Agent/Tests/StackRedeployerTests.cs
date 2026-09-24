using System.Net;
using System.Text.Json;
using EggIdentity.Contract;
using EggIdentity.Deploy;

namespace EggIdentity.Agent.Tests;

public class StackRedeployerTests {
    private static readonly PortainerConfig Config = new("https://portainer.test", "key");
    private static readonly DeployStack GitRow = new() { Name = "egg-apps", StackId = 56, EndpointId = 9 };
    private static readonly DeployStack FileRow = new() { Name = "db", StackId = 3, EndpointId = 9 };
    private static readonly Uri WebhookUrl = new($"https://portainer.test/api/stacks/webhooks/{PortainerFakes.Webhook}");

    private static (StackRedeployer Redeployer, FakePortainerHandler Handler) Build(bool configured = true) {
        var handler = new FakePortainerHandler();
        return (new StackRedeployer(new HttpClient(handler), configured ? Config : null), handler);
    }

    private static StackReadiness Ready(DeployStack row, bool git, Uri? webhook) =>
        new(new StackInfo(row.Name, row.StackId, row.EndpointId, "ei-servers", git, null, null, git, git, null), webhook);

    [Fact]
    public async Task Resolve_NoPortainerConfig_RefusesWithoutHttp() {
        var (redeployer, handler) = Build(configured: false);

        var readiness = await redeployer.ResolveAsync(GitRow, CancellationToken.None);

        Assert.False(readiness.Ready);
        Assert.Contains("portainer.api_url", readiness.Info.Refusal!, StringComparison.Ordinal);
        Assert.Null(readiness.WebhookUrl);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Resolve_RowWithoutIds_Refuses() {
        var (redeployer, handler) = Build();

        var readiness = await redeployer.ResolveAsync(new DeployStack { Name = "x" }, CancellationToken.None);

        Assert.False(readiness.Ready);
        Assert.Contains("stack_id", readiness.Info.Refusal!, StringComparison.Ordinal);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Resolve_GitStackArmedAndForced_IsReadyWithWebhookUrl() {
        var (redeployer, handler) = Build();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack);

        var readiness = await redeployer.ResolveAsync(GitRow, CancellationToken.None);

        Assert.True(readiness.Ready);
        Assert.Equal(WebhookUrl, readiness.WebhookUrl);
        Assert.Equal("egg-apps", readiness.Info.Name);
        Assert.Equal("ei-servers", readiness.Info.PortainerName);
        Assert.True(readiness.Info.GitBacked);
        Assert.True(readiness.Info.WebhookArmed);
        Assert.True(readiness.Info.ForceUpdate);
        Assert.Equal("https://gitea:3000/EIStacks/ei-servers.git", readiness.Info.RepositoryUrl);
        Assert.Equal("refs/heads/main", readiness.Info.ReferenceName);
        Assert.Equal(["GET /api/stacks/56"], handler.Calls);
    }

    [Fact]
    public async Task Resolve_GitStackWithoutWebhook_Refuses() {
        var (redeployer, handler) = Build();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack.Replace(PortainerFakes.ArmedAutoUpdate, "null", StringComparison.Ordinal));

        var readiness = await redeployer.ResolveAsync(GitRow, CancellationToken.None);

        Assert.False(readiness.Ready);
        Assert.Contains("GitOps webhook", readiness.Info.Refusal!, StringComparison.Ordinal);
        Assert.Null(readiness.WebhookUrl);
        Assert.False(readiness.Info.WebhookArmed);
    }

    [Fact]
    public async Task Resolve_GitStackWithoutForceUpdate_Refuses() {
        var (redeployer, handler) = Build();
        handler.On("GET", "/api/stacks/56", PortainerFakes.GitStack.Replace("\"ForceUpdate\":true", "\"ForceUpdate\":false", StringComparison.Ordinal));

        var readiness = await redeployer.ResolveAsync(GitRow, CancellationToken.None);

        Assert.False(readiness.Ready);
        Assert.Contains("force redeploy", readiness.Info.Refusal!, StringComparison.Ordinal);
        Assert.Null(readiness.WebhookUrl);
        Assert.True(readiness.Info.WebhookArmed);
        Assert.False(readiness.Info.ForceUpdate);
    }

    [Fact]
    public async Task Resolve_WebEditorStack_IsReadyWithoutWebhook() {
        var (redeployer, handler) = Build();
        handler.On("GET", "/api/stacks/3", PortainerFakes.WebEditorStack());

        var readiness = await redeployer.ResolveAsync(FileRow, CancellationToken.None);

        Assert.True(readiness.Ready);
        Assert.Null(readiness.WebhookUrl);
        Assert.False(readiness.Info.GitBacked);
        Assert.Equal("db", readiness.Info.PortainerName);
    }

    [Fact]
    public async Task Redeploy_GitStack_PostsWebhookWithEmptyBody() {
        var (redeployer, handler) = Build();
        handler.On("POST", WebhookUrl.PathAndQuery, status: HttpStatusCode.Accepted);

        await redeployer.RedeployAsync(GitRow, Ready(GitRow, git: true, WebhookUrl), CancellationToken.None);

        Assert.Equal(["POST " + WebhookUrl.PathAndQuery], handler.Calls);
        Assert.Equal(WebhookUrl, handler.Write!.RequestUri);
        Assert.True(string.IsNullOrEmpty(handler.WriteBody));
    }

    [Fact]
    public async Task Redeploy_WebhookConflict_ThrowsStackBusy() {
        var (redeployer, handler) = Build();
        handler.On("POST", WebhookUrl.PathAndQuery, status: HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<StackBusyException>(
            () => redeployer.RedeployAsync(GitRow, Ready(GitRow, git: true, WebhookUrl), CancellationToken.None));
    }

    [Fact]
    public async Task Redeploy_WebhookServerError_ThrowsWithStatusAndBodyButNotWebhookId() {
        var (redeployer, handler) = Build();
        handler.On("POST", WebhookUrl.PathAndQuery, "stack deploy failed", HttpStatusCode.InternalServerError);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => redeployer.RedeployAsync(GitRow, Ready(GitRow, git: true, WebhookUrl), CancellationToken.None));

        Assert.Contains("500", error.Message, StringComparison.Ordinal);
        Assert.Contains("stack deploy failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(PortainerFakes.Webhook, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redeploy_WebEditorStack_PutsStackUpdate() {
        var (redeployer, handler) = Build();
        handler.On("GET", "/api/stacks/3", PortainerFakes.WebEditorStack())
            .On("GET", "/api/stacks/3/file", """{"StackFileContent":"services: {}"}""")
            .On("PUT", "/api/stacks/3?endpointId=9");

        await redeployer.RedeployAsync(FileRow, Ready(FileRow, git: false, null), CancellationToken.None);

        Assert.Equal(["GET /api/stacks/3", "GET /api/stacks/3/file", "PUT /api/stacks/3?endpointId=9"], handler.Calls);
        Assert.Equal("/api/stacks/3?endpointId=9", handler.Write!.RequestUri!.PathAndQuery);
        Assert.False(PortainerFakes.Body(handler).GetProperty("RepullImageAndRedeploy").GetBoolean());
    }

    [Fact]
    public async Task Redeploy_NotReady_Throws() {
        var (redeployer, handler) = Build();
        var refused = new StackReadiness(new StackInfo("egg-apps", 56, 9, null, false, null, null, false, false, "not today"), null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => redeployer.RedeployAsync(GitRow, refused, CancellationToken.None));

        Assert.Equal("not today", error.Message);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void Describe_UsesRowNameAndPortainerName() {
        using var doc = JsonDocument.Parse(PortainerFakes.GitStack.Replace(PortainerFakes.ArmedAutoUpdate, "null", StringComparison.Ordinal));

        var info = StackRedeployer.Describe(GitRow, PortainerClient.ReadStack(doc.RootElement));

        Assert.Contains("""stack "egg-apps" (Portainer #56 ei-servers)""", info.Refusal!, StringComparison.Ordinal);
    }
}
